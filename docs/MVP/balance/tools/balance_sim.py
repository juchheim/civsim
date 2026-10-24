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
    lean = food * min(1.0, (s["LeanBase"] + A_["LeanShareAdjustment"]) * m["Lean"])
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
        # Fission is checked against the population at the end of this season.
        # In particular, 09 §7.2 defines the lean-food check at Winter's end,
        # after winter births, deaths and cohort aging. Keep the previous-row
        # population for this season's rates and crowding, but use the resulting
        # population for both pressure triggers, as the live model does.
        per = newpop / prev["S"]
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


# =====================================================================================
# Live mode (docs/MVP/22 §15.2 item 1; findings in 07d §2–§3)
#
# The mean-field model above describes a smoothed demography (C/56 aging, identical
# settlements, food-bound caps). The live sim differs in three measured ways (07c §7a,
# 07d §2): exact ages, InsightCalibration 1.6, and settlements that are bounded by
# just-in-time housing (they bud at ~45 people while FoodCap is 100–190). Live mode
# keeps the research accounting of `era_derived` and replaces the demography with
# per-settlement exact-age cohorts, the live housing rule, live roles (one Elder per
# settlement with an elder, one Shaman per settlement with six adults) and E3 roles by
# structure (one Priest per Temple, Scribes per Palace and School, +8 a year per City).
# Validated against tools/live_data.py (8 seeds × 4 Balanced civs to minute 64).
#
#   python balance_sim.py live              # E3 report and T1–T10 table (both housing regimes)
#   python balance_sim.py live --validate   # E2-economy run vs the live CSV
#   python balance_sim.py live --check      # assertions (used by 07d)
# =====================================================================================
LIVE = dict(
    InsightCalibration=1.6,        # content/constants/balance.yaml (07c §7a)
    KHOfCommons=0.314,             # Know-how per calibrated Commons Insight: live share 17.6% (07d §2)
    # Requirement multiplier per era. E0–E2 overheads fit the pre-cadence live baseline; E2 now costs more, so
    # KEY-3 is intentionally later. E3: the measured live E2 spend ÷ oracle
    # requirement (1.20: the Council's extra picks) × the model's progress ÷ Insight (1.21) ÷ the live one (1.16).
    Overhead=[1.422, 1.398, 1.328, 1.25],
    EraBase=[22, 50, 100, 160],    # 07c §10: E2 cadence raised; E3 stays near 13 min in the updated live projection
    BackfillOverlap=0.30,
    FoodFactor=1.35,               # live FoodCap ÷ 09 target (median 120 vs 88 at E2 end, 07d §2)
    Dwelling=[4, 6, 8, 8],         # capacity of the dwelling the planner builds, by era (lean-to, pit house, wattle, mudbrick)
    Buffer=8,                      # planner builds when HousingCap < pop + 8 (LivingEconomyModel.SelectBuildCandidate)
    VillageDwellings=1,            # dwellings a just-in-time village gets built before it buds (live eff. cap 40 + 8 = 48)
    RolesAt=(2.0, 2.4),            # research position of the live Elder (GOV-01) and Shaman (BEL-03) role output (07d §2)
    Regime="village",              # "village": every settlement just-in-time (live today); "towns": a Market town
                                   # builds housing ahead to its FoodCap and buds only at 85% of FoodCap (07d R2)
    UrbanPull=0.0,                 # share of village fissions that join an own Market town with room (07d R2)
    MarketEvery=4,                 # with TRD-04: a Market in the largest settlement, one more per 4 settlements
    TownPop=60, CityPop=120,       # tiers (22 §8.1)
    E3=True,                       # E3 caps, roles and structures on; False = the live sim today (MaxEra 2)
    Smax=30,
    Fertility=0.13, Crowding=6,
    CommonsMult=1.10,              # live Commons ÷ formula at the live headcount (hearth share, Era Boons; 07d §2)
    HazardMult=1.07,
    E3InsightMult=1.0,             # sensitivity only: Insight × this from KEY-3 on               # live excess mortality (ecology, shortfalls, epidemics) on every hazard
)
# E3 (and KEY-4 backfill) projects by research position: (id, at, take probability). Cap projects keep 09's
# placement (params.CAPS); these are the structure and role projects (doc 22 §12, closure = prob 1).
LIVE_E3 = {"TRD-04": (0.30, 1.0), "COM-05": (0.40, 1.0), "SOC-08": (0.45, 1.0), "SOC-09": (0.60, 1.0),
           "GOV-05": (0.75, 1.0), "GOV-06": (0.85, 1.0), "BEL-06": (0.55, 0.6), "LRN-04": (0.65, 0.4),
           "COM-06": (0.70, 0.4), "NUM-06": (0.80, 0.3), "BLD-07": (0.50, 0.6), "WOD-05": (0.35, 1.0)}
LIVE_HAZ = dict(child=[.036, .033, .035, .034], adult=[.012, .011, .012, .011], h45=[.023, .021, .021, .018],
                winter=[2.0, 1.7, 1.4, 1.25])
LIVE_ROSTER = [(6, 1), (9, 0), (18, 1), (20, 0), (22, 1), (24, 0), (26, 1), (28, 0), (30, 1), (32, 0), (50, 1), (55, 0)]


def live_requirements(cfg):
    """Per-era research requirement (progress units). E0–E2 from the catalog as `era_derived`; E3 from doc 22
    §12.3: 18 in-era closure projects (21.4 type units), 3 free picks, KEY-4 (K) and the 20 listed backfills."""
    s = scenario_for_live(cfg)
    D = era_derived(s)
    req = [D[e]["req"] for e in range(3)]
    eb = cfg["EraBase"]
    picks = 21.4 + 3 * CAT[3]["free_avg_tm"]
    key_spend = eb[3] * s["KeystoneMult"] * 7 / 4
    bf = (1 - cfg["BackfillOverlap"]) * (1.0 * eb[0] * 0.7 ** 3 + 9.2 * eb[1] * 0.7 ** 2 + 10.2 * eb[2] * 0.7)
    req.append(eb[3] * picks + key_spend + bf)
    return [r * o for r, o in zip(req, cfg["Overhead"])], D, s


def scenario_for_live(cfg):
    eb = list(P.ERA["EraBase"]["tuned"])
    eb[:4] = cfg["EraBase"]
    return P.scenario("tuned", overrides={"EraBase": eb})


def _hazard_grid(e, np):
    H = LIVE_HAZ
    age = np.arange(100)
    child = np.where(age == 0, 2.333333333, np.where(age < 5, 1.166666667, 0.777777778)) * H["child"][e]
    elder = np.minimum(4.0, H["h45"][e] * 1.08872 ** (age - 45))
    base = np.where(age < 14, child, np.where(age < 45, H["adult"][e] * 0.94, elder))
    g = np.repeat(base[:, None, None], 2, axis=1).repeat(4, axis=2).copy()
    fert = 0.5 * LIVE["Fertility"] * P.ERA["FertMult"]["tuned"][e]
    g[14:45, 0, 1] += H["adult"][e] * 0.06 / fert   # maternal hazard on the delivering season (exact_age_sensitivity)
    return g


def run_live(cfg=None, minutes=70):
    import numpy as np
    cfg = dict(LIVE, **(cfg or {}))
    req, D, s = live_requirements(cfg)
    grids = [_hazard_grid(e, np) for e in range(4)]
    cal = cfg["InsightCalibration"]

    def new_settlement(coh, H=40.0):
        return dict(coh=coh, H=H, last=-4, pending=False, market=False)

    c0 = np.zeros((100, 2, 4))
    for a, fem in LIVE_ROSTER:
        c0[a, 0 if fem else 1, 0] += 1
    setts = [new_settlement(c0)]
    era, prog = 0, 0.0
    rows, done_at = [], {}
    migrate_acc, food_prev, group_prev = 0.0, 1e9, 1e9
    for t in range(1, int(minutes * 4) + 1):
        pos_e = min(3, era)
        pos = era + min(0.999, prog / req[era]) if era < 4 else 4.0
        e = min(3, era)
        cp = caps(s, e, min(pos, 3.999))
        if not cfg["E3"] and pos >= 3:
            cp = caps(s, 2, 2.999)
        food = cp["food"] * cfg["FoodFactor"]
        lean_share = cp["lean"] / cp["food"]
        group = cp["group"]

        def done(pid):
            if pid in LIVE_E3:
                at, pr = LIVE_E3[pid]
                return (pos >= 3 + at) * pr if cfg["E3"] else 0.0
            return 1.0
        # resolve last season's fissions
        born = []
        for st in setts:
            if st["pending"]:
                if t - 1 - st["last"] >= 4 and len(setts) + len(born) < cfg["Smax"] and st["coh"].sum() * 0.4 >= 6:
                    st["coh"] = st["coh"] * 0.6
                    st["last"] = t - 1
                    daughter = st["coh"] * (2 / 3)
                    # urban pull (07d R2): a share of village fissions joins an own Market town with room instead
                    towns_room = [x for x in setts if x["market"] and x is not st
                                  and x["coh"].sum() + daughter.sum() < 0.85 * min(food_prev, group_prev)]
                    migrate_acc += cfg["UrbanPull"] if (towns_room and not st["market"]) else 0.0
                    if towns_room and migrate_acc >= 1.0:
                        migrate_acc -= 1.0
                        towns_room[0]["coh"] += daughter
                    else:
                        born.append(new_settlement(daughter))
                        born[-1]["last"] = t - 1
                st["pending"] = False
        setts += born
        winter = t % 4 == 0
        wm = LIVE_HAZ["winter"][e] if winter else 1.0
        fert = cfg["Fertility"] * P.ERA["FertMult"]["tuned"][e]
        pconc = 1.0 / (1.0 / fert - 3.0)
        dwell = cfg["Dwelling"][e]
        # markets (TRD-04): the largest settlements, one per MarketEvery
        if cfg["Regime"] == "towns" and done("TRD-04") > 0.5:
            want = 1 + (len(setts) - 1) // cfg["MarketEvery"]
            order = sorted(range(len(setts)), key=lambda i: -setts[i]["coh"].sum())
            for i in order[:want]:
                setts[i]["market"] = True
        for st in setts:
            coh = st["coh"]
            pop0 = coh.sum()
            coh *= 1 - np.minimum(1.0, grids[e] * cfg["HazardMult"] * wm / 4)
            mcap = min(food, st["H"])
            crowd = min(1.0, (pop0 / mcap) ** cfg["Crowding"])
            fem = coh[:, 0, :]
            births = fem[:, 1].sum()
            elig = fem[:, 0].copy()
            fem[:, 0] += fem[:, 1]
            fem[:, 1] = fem[:, 2]
            fem[:, 2] = fem[:, 3]
            conc = np.zeros(100)
            conc[14:45] = elig[14:45] * pconc * (1 - crowd)
            fem[:, 0] -= conc
            fem[:, 3] = conc
            if winter:
                aged = np.zeros_like(coh)
                aged[1:] = coh[:-1]
                aged[99] += coh[99]
                coh[:] = aged
            coh[0, 0, 0] += births * 0.5
            coh[0, 1, 0] += births * 0.5
            pop = coh.sum()
            # housing: just-in-time (live planner), or built ahead to the food cap in a Market town
            target = pop + cfg["Buffer"]
            if st["market"]:
                target = max(target, 0.85 * min(food, group) + cfg["Buffer"])
            builds = 0
            cap_h = 10 ** 6 if st["market"] else 40.0 + dwell * cfg["VillageDwellings"]
            while st["H"] < target and builds < (3 if st["market"] else 1) and st["H"] + dwell <= cap_h:
                st["H"] += dwell
                builds += 1
            fis_cap = min(food, group) if st["market"] else min(food, st["H"], group)
            cap_pressure = pop >= 0.85 * fis_cap
            late = winter and pop >= food * lean_share
            if t - 1 - st["last"] >= 4 and (cap_pressure or late) and pop * 0.4 >= 6:
                st["pending"] = True
        # Insight
        A = sum(st["coh"][14:45].sum() for st in setts)
        El = sum(st["coh"][45:].sum() for st in setts)
        lm = D[e]["lm"]
        commons = (cfg["CommonsMult"] * s["CommonsCoef"] * (A + s["ElderCommons"] * lm["Elder"] * El) ** s["CommonsExp"]
                   * lm["Commons"])
        # GOV-01 elders and BEL-03 shamans produce from the positions the live Council reaches them (07d §2)
        elders_set = sum(1 - math.exp(-st["coh"][45:].sum()) for st in setts)
        shamans_set = sum(1.0 for st in setts if st["coh"][14:45].sum() >= 6)
        e2roles = (pos >= cfg["RolesAt"][0]) * elders_set * 8 * lm["Elder"] + (pos >= cfg["RolesAt"][1]) * shamans_set * 12
        pops = sorted((st["coh"].sum() for st in setts), reverse=True)
        towns = sum(1 for st, p in zip(sorted(setts, key=lambda x: -x["coh"].sum()), pops)
                    if p >= cfg["TownPop"] and (st["market"] or cfg["Regime"] == "village" and done("TRD-04") > 0.5))
        cities = sum(1 for p in pops if p >= cfg["CityPop"]) * (done("SOC-09") > 0.5) * (done("BEL-06") > 0 or done("GOV-05") > 0.5)
        cities = min(cities, towns)
        temples = done("BEL-06") * towns                      # one Temple per Town and City (BEL-06: Towns and Cities)
        palace = 1.0 if done("GOV-05") > 0.5 else 0.0          # the Palace at the capital (GOV-05)
        scribes = (done("COM-05") > 0.5) * (palace + 2 * done("LRN-04")) * (1 + 0.5 * done("COM-06"))  # 1 + 2 per School
        spec = 1 + 0.10 * done("SOC-08")
        e3roles = (temples * (16 + 4 * done("NUM-06")) + scribes * 24) * spec + 8 * cities
        e3m = cfg["E3InsightMult"] if era >= 3 else 1.0
        insight = e3m * cal * (commons + e2roles + e3roles) * lm["All"] * D[e]["boon"]
        kh = e3m * cfg["KHOfCommons"] * cal * commons * lm["All"]
        progress = (insight + kh) / 4
        if era < 4:
            prog += progress
            if prog >= req[era]:
                prog -= req[era]
                era += 1
                done_at[era] = t / 4
        food_prev, group_prev = food, group
        rows.append(dict(t=t, minute=t / 4, era=e, pos=pos, pop=sum(pops), A=A, El=El, S=len(setts), pops=pops,
                         insight=insight, commons=cal * commons * lm["All"] * D[e]["boon"],
                         e2roles=cal * e2roles * lm["All"] * D[e]["boon"], e3roles=cal * e3roles * lm["All"] * D[e]["boon"],
                         kh=kh, towns=towns, cities=cities, food=food,
                         H=sum(st["H"] for st in setts) / len(setts)))
    return rows, done_at, req


def live_summary(rows, done_at, req):
    k3, k4 = done_at.get(3), done_at.get(4)
    out = dict(k1=done_at.get(1), k2=done_at.get(2), k3=k3, k4=k4, e3=(k4 - k3) if k3 and k4 else None)
    if k3 and k4:
        win = [r for r in rows if k3 < r["minute"] <= k4]
        at3 = min(rows, key=lambda r: abs(r["minute"] - k3))
        at4 = min(rows, key=lambda r: abs(r["minute"] - k4))
        ins = sum(r["insight"] for r in win) / 4
        out.update(ins_k3=at3["insight"], ins_k4=at4["insight"], ins_mean=ins / (k4 - k3),
                   kh_share=sum(r["kh"] for r in win) / sum(r["kh"] + r["insight"] for r in win),
                   e3_roles_share=at4["e3roles"] / at4["insight"], e2_roles_share=at4["e2roles"] / at4["insight"],
                   pop_k4=at4["pop"], S_k4=at4["S"], towns_k4=at4["towns"], cities_k4=at4["cities"],
                   biggest_k4=at4["pops"][0], mean_size_k4=at4["pop"] / at4["S"])
    return out


def fit_live_erabase(cfg=None, target=13.0):
    """Bisect EraBase[3] so the live-mode E3 duration (own KEY-3 -> own KEY-4) equals `target` minutes."""
    lo, hi = 120.0, 320.0
    for _ in range(14):
        mid = (lo + hi) / 2
        c = dict(LIVE, **(cfg or {}))
        c["EraBase"] = list(c["EraBase"])
        c["EraBase"][3] = mid
        r = live_summary(*run_live(c, minutes=75))
        if r["e3"] is None or r["e3"] > target:
            hi = mid
        else:
            lo = mid
    return (lo + hi) / 2


def fit_live_overhead(cfg=None, targets=(6.6, 20.8, 34.0)):
    """Fit Overhead[0..2] so live-mode KEY-1/2/3 land on the live medians (tools/live_data.py); E3 takes E2's."""
    c = dict(LIVE, **(cfg or {}))
    ov = list(c["Overhead"])
    for e in range(3):
        lo, hi = 0.7, 2.0
        for _ in range(18):
            mid = (lo + hi) / 2
            o = list(ov)
            o[e] = mid
            _, d, _ = run_live(dict(c, E3=False, Overhead=o), 45)
            lo, hi = (mid, hi) if d.get(e + 1, 99) < targets[e] else (lo, mid)
        ov[e] = (lo + hi) / 2
    ov[3] = round(LIVE_E2_SPEND_RATIO * 1.21 / 1.16, 3)
    return ov


LIVE_E2_SPEND_RATIO = 1.20   # live progress spent between KEY-2 and KEY-3 ÷ oracle E2 requirement (median of 32 civs)


def live_validate(out=print):
    """E2 economy only (MaxEra 2, as the live sim today) against the live CSV (tools/live_data.py)."""
    import live_data as L
    import statistics as st
    live = L.load()
    ks = L.keystones(live)
    rows, done_at, _ = run_live(dict(E3=False), minutes=64)
    out("Live mode vs the live sim (E2 economy; live = 8 seeds × 4 civs mean):")
    out(f"  KEY-1/2/3: model {done_at.get(1):.1f} / {done_at.get(2):.1f} / {done_at.get(3):.1f};"
        f" live median {st.median(ks['k1']):.1f} / {st.median(ks['k2']):.1f} / {st.median(ks['k3']):.1f}")
    out("  min   pop model/live   settl model/live   biggest model/live   Insight/yr model/live   roles% model/live")
    res = []
    for m in (20, 30, 35, 40, 45, 50, 55, 60):
        r = min(rows, key=lambda x: abs(x["minute"] - m))
        lr = L.by_minute(live, m)
        lp, lS = st.mean(x["pop"] for x in lr), st.mean(x["S"] for x in lr)
        lbig = st.mean(max(x["pops"]) for x in lr)
        lins = st.mean(x["ins"] for x in lr)
        lrol = st.mean((x["ins"] - x["com"]) / x["ins"] for x in lr)
        res.append((m, r["pop"] / lp, r["S"] / lS, r["insight"] / lins))
        out(f"  {m:3d}   {r['pop']:5.0f} / {lp:5.0f}      {r['S']:3d} / {lS:4.1f}         {r['pops'][0]:4.0f} / {lbig:4.0f}"
            f"          {r['insight']:5.0f} / {lins:5.0f}           {100 * (r['e2roles'] / r['insight']):4.1f} / {100 * lrol:4.1f}")
    return done_at, res, ks


def mandate_earned(minutes=13.0, com05_at=0.40, com06=(0.70, 0.4), bel07=(0.90, 0.3)):
    """Expected Mandate earned over an E3 of `minutes` (22 §4.2): start 1, +1 every R seasons, R = 8 − 2 (COM-05)
    − 1 (COM-06) − 1 (BEL-07), never at the cap (the AI spends before it)."""
    seasons = int(round(minutes * 4))
    earned, acc = 1.0, 0.0
    for k in range(seasons):
        x = k / seasons
        r = 8 - 2 * (x >= com05_at) - com06[1] * (x >= com06[0]) - bel07[1] * (x >= bel07[0])
        acc += 1 / max(4.0, r)
    return earned + acc


def live_targets(cfg, out=print, label=""):
    """T1–T10 at one configuration."""
    r = live_summary(*run_live(cfg, minutes=75))
    s = scenario_for_live(dict(LIVE, **cfg))
    n_done = 21 + era_derived(s)[3]["spill"]
    backfills = 20 * (1 - LIVE["BackfillOverlap"])
    m = mandate_earned(r["e3"])
    rows = [
        ("T1", "KEY-3 (min)", f"{r['k3']:.1f}", "29 ±25% (21.8–36.2)", 21.75 <= r["k3"] <= 36.25),
        ("T2", "E3 duration (min)", f"{r['e3']:.2f}", "13.0; median 11.5–14.5", 11.5 <= r["e3"] <= 14.5),
        ("T3", "KEY-4 (min)", f"{r['k4']:.1f}", "43 ±25% (32.3–53.8)", 32.25 <= r["k4"] <= 53.75),
        ("T5", "Insight/min at KEY-3 -> KEY-4", f"{r['ins_k3']:.0f} -> {r['ins_k4']:.0f}", "~330 -> 650–800 ±20%",
         abs(r["ins_k3"] / 330 - 1) <= 0.2 and 520 <= r["ins_k4"] <= 960),
        ("T6", "Know-how share", f"{100 * r['kh_share']:.1f}%", "13–20%", 0.13 <= r["kh_share"] <= 0.20),
        ("T7", "E3 roles share of Insight at KEY-4", f"{100 * r['e3_roles_share']:.1f}%", "12–20%",
         0.12 <= r["e3_roles_share"] <= 0.20),
        ("T8", "in-era completions / backfills", f"{n_done:.1f} / {backfills:.1f}", "22–25 / 11–20",
         22 <= n_done <= 25 and 11 <= backfills <= 20),
        ("T9", "s per in-era completion (07 F10); Mandate", f"{r['e3'] * 60 / n_done:.0f} s ({r['e3'] * 60 / (n_done + backfills):.0f} "
         f"with backfills); {m:.1f}", "25–35 s; 7–9 per E3", 25 <= r["e3"] * 60 / n_done <= 35 and 7 <= m <= 9),
        ("T10", "people per civ at KEY-4", f"{r['pop_k4']:.0f}", "220 (170–300)", 170 <= r["pop_k4"] <= 300),
        ("T11", "settlements per civ at KEY-4", f"{r['S_k4']} (biggest {r['biggest_k4']:.0f}; towns {r['towns_k4']}, "
         f"cities {r['cities_k4']})", "7 (5–10)", 5 <= r["S_k4"] <= 10),
    ]
    out(f"  -- {label}")
    for tid, name, got, tgt, ok in rows:
        out(f"  {tid:4s} {name:38s} {got:34s} target {tgt:28s} {'ok' if ok else 'MISS'}")
    return r, rows


def live_report(out=print):
    out("== balance_sim live mode: E3 at Standard pace, four Balanced civs (doc 22 §15.1 T1–T11)")
    for regime in ("village", "towns"):
        for eb in (160, 200):
            r = live_summary(*run_live(dict(Regime=regime, EraBase=LIVE["EraBase"][:3] + [eb]), minutes=75))
            out(f"  {regime:7s} EraBase {eb}: E3 {r['e3']:.2f} min (KEY-3 {r['k3']:.2f}, KEY-4 {r['k4']:.2f})")
    fits = {}
    for reg in ("village", "towns"):
        fits[reg] = fit_live_erabase(dict(Regime=reg))
        out(f"  fitted EraBase[3] for a 13.0-min E3 ({reg} housing): {fits[reg]:.1f} -> {int(round(fits[reg] / 10) * 10)}")
    eb = LIVE["EraBase"][3]  # current cadence projection after raising E2's EraBase
    out(f"\n  T-table at EraBase[3] {eb}:")
    tabs = {}
    for reg in ("village", "towns"):
        tabs[reg] = live_targets(dict(Regime=reg, EraBase=LIVE["EraBase"][:3] + [eb]), out,
                                 f"{reg} housing ({'live planner today' if reg == 'village' else 'R2: Market towns build ahead'})")
    out("\n  Sensitivity of the E3 duration to a 20% error (towns housing, EraBase " + str(eb) + "):")
    base = live_summary(*run_live(dict(Regime="towns", EraBase=LIVE["EraBase"][:3] + [eb]), minutes=75))["e3"]
    for name, c in (("E3-window Insight ×0.8", dict(E3InsightMult=0.8)), ("E3-window Insight ×1.2", dict(E3InsightMult=1.2)),
                    ("all Insight ×0.8 (KEY-3 moves too)", dict(CommonsMult=LIVE["CommonsMult"] * 0.8)),
                    ("Council overhead 1.0 (no extra picks)", dict(Overhead=LIVE["Overhead"][:3] + [1.0 * 1.21 / 1.16])),
                    ("Council overhead +20%", dict(Overhead=LIVE["Overhead"][:3] + [1.5])),
                    ("Fertility ×0.8", dict(Fertility=0.104)), ("Fertility ×1.2", dict(Fertility=0.156)),
                    ("E3 roles off", dict(E3=False))):
        r = live_summary(*run_live(dict(dict(Regime="towns", EraBase=LIVE["EraBase"][:3] + [eb]), **c), minutes=80))
        out(f"    {name:40s} E3 {r['e3']:.2f} min ({100 * (r['e3'] / base - 1):+.0f}%), KEY-3 {r['k3']:.2f}, people {r['pop_k4']:.0f}")
    return tabs, fits


def live_check():
    done_at, res, ks = live_validate(out=lambda *_: None)
    import statistics as st
    for m, rp, rS, ri in res:
        if m >= 30:
            assert 0.75 <= rp <= 1.30, (m, "pop", rp)
            assert 0.60 <= rS <= 1.50, (m, "settlements", rS)
            assert 0.75 <= ri <= 1.30, (m, "insight", ri)
    assert abs(done_at[3] - st.median(ks["k3"])) <= 3.0, done_at
    r = live_summary(*run_live(dict(Regime="towns", EraBase=LIVE["EraBase"][:3] + [140]), minutes=75))
    assert 11.5 <= r["e3"] <= 14.5, r
    r200 = live_summary(*run_live(dict(Regime="towns", EraBase=LIVE["EraBase"][:3] + [200]), minutes=80))
    assert r200["e3"] > 14.5, ("EraBase 200 is too slow in live mode", r200)
    assert 0.13 <= r["kh_share"] <= 0.20, r
    print("balance_sim live check passed")


if __name__ == "__main__":
    args = sys.argv[1:]
    if args and args[0] == "live":
        if "--validate" in args:
            live_validate()
        elif "--check" in args:
            live_check()
        else:
            live_validate()
            live_report()
    elif args:
        report(*args)
    else:
        report("v01")
        report("tuned")
