"""E3 metal and fuel oracle (docs/MVP/22 §5, §15 T17–T20; findings in 07d §5).

One settlement, season by season, from KEY-3 to KEY-4 and beyond: adults wear their tool kits by tool work, the
planner casts replacement and upgrade kits of the best alloy it can feed (best rung known and supplied, 05 §7.2),
and the chain runs in 22 §5.3's fixed order: mine ore -> burn charcoal (wood from the range's forest stock) ->
smelt -> alloy -> cast. Metal labor is capped at a share of the settlement's work. Forest tiles hold 30 L, regrow
0.5 L a season (×2 with WOD-04 Coppicing) plus 2 L/yr of deadwood; a tile below 5 L is cleared (22 §5.5).
Access is per settlement (22 §5.7): without tin no T10 kit is cast and existing kits wear out.

    uv run --no-project --with numpy python metal_sim.py            # T17–T20 report and sensitivities
    uv run --no-project --with numpy python metal_sim.py --check    # assertions
"""
import sys

# ---- 22 §5.3 recipes, per L of metal out ----------------------------------------------------
KILN = dict(ore=4.0, char=4.0, ws=1.0)              # 2 L ore + 2 L charcoal -> 0.5 L, 0.5 ws
BELLOWS = dict(ore=2 / 0.7, char=1.6 / 0.7, ws=0.5 / 0.7)   # FIR-09: 2 L ore + 1.6 L charcoal -> 0.7 L
TIN_SMELT = dict(ore=2.0, char=2.0, ws=0.6)         # 1 L tin ore + 1 L charcoal -> 0.5 L tin, 0.3 ws
ALLOY = dict(cu=0.9, tin=0.1, char=0.5, ws=0.2)     # per L of tin bronze
CHAR = dict(wood=6.0, ws=0.4)                       # per L of charcoal
KIT = {"T8": dict(metal=0.12, dur=12, alloy="copper"), "T9": dict(metal=0.15, dur=16, alloy="arsenical"),
       "T10": dict(metal=0.15, dur=24, alloy="tin"), "T7": dict(metal=0.0, dur=10, alloy="stone")}
CAST_WS = 0.1
ARMS = dict(metal=0.3, wood=1.0, ws=0.3)
WOOD_PER_WS = 2.0                                   # 10 §5 base rate
CHOP = {"T7": 4.0, "T8": 4.5, "T9": 6.0, "T10": 6.0}
MINE = {"surface": 1.0, "shaft": 1.5, "placer": 0.5}
PICKS = {"T7": 1.0, "T8": 1.1, "T9": 1.3, "T10": 1.3}

# ---- settlement (ASSUMED where 22 and 10 are silent; see 07d §5) -----------------------------
P = dict(
    adult_share=0.35,           # 22 §5.6 (live mix ~33–35%)
    ws_per_adult=38 / 35,       # 22 §5.6: 100 people, 35 adults, 38 ws a season
    tool_share=0.60,            # share of work done with a tool kit (chop, farm, build, mine, craft)
    forest_tiles=13,            # radius-3 range (37 tiles), ~35% forest (22 §5.5)
    forest_stock=30.0, regrow=0.5, deadwood=2.0, cleared_below=5.0,
    hearth_per=35,              # one hearth (1.5 L a season, 3 in Winter) per 35 people (10 §5)
    kiln_wood=0.20,             # L of kiln-firing wood per person per year (pottery, lime; 10 §5: 4 L a batch)
    build_wood=0.15,            # L of building wood per person per year (dwellings, stores, repairs)
    metal_cap=0.10,             # most of a season's work the planner gives to the metal chain
    warriors_per100=4, arms_life_years=6, battle_loss=0.05,  # 22 §5.6: arms ≈ 0.15 L a year per 100
    remelt=0.5,                 # doc 23 §4.1 UpgradeRemeltShare
    dormancy_tail=1.4,          # doc 23 §4.1: kits worked from quality 0.7 down to 0.5 when they cannot be replaced
    adopt_seasons=3,            # 02 §10: a new method spreads in 2–4 seasons
    job_tool_shares=(0.3, 0.5, 0.7, 0.9),   # tool work share by job (crafters, herders, farmers, choppers/miners); mean 0.6
)
# research positions (share of E3) at which the metal projects complete for an average civ (22 §12 closure)
AT = dict(MET03=0.20, FIR09=0.45, MET05=0.40, MET04=0.45, MIN04=0.50, WAR05=0.60, WOD04=None)


def season_pos(t, seasons):
    return t / seasons


def run(pop=100.0, seasons=52, access=("tin", "copper"), bellows=True, coppice=False, metal_cap=None,
        cut_tin_at=None, pop_growth=0.0, warriors=True, start_stock=1.0, p=None):
    """Simulate one settlement over an E3 of `seasons`. access: the alloys it can feed ("tin", "arsenic", "copper").
    Returns per-season rows."""
    p = dict(P, **(p or {}))
    cap_share = p["metal_cap"] if metal_cap is None else metal_cap
    forest = [p["forest_stock"] * start_stock] * p["forest_tiles"]
    kits = {"T7": 0.0, "T8": 0.0, "T9": 0.0, "T10": 0.0}   # durability-weighted: list of (tier, remaining ws) cohorts
    cohorts = []                                          # (tier, count, remaining ws)
    adults = pop * p["adult_share"]
    jobs = p["job_tool_shares"]                           # tool work varies by job: wear rates differ (07d §5)
    for js in jobs:
        cohorts.append(["T7", adults / len(jobs), 5.0, js])   # polished stone kits, half worn
    rows = []
    remelt_pool = 0.0
    totals = dict(bronze=0.0, copper=0.0, tin=0.0, char=0.0, metal_wood=0.0, ws=0.0, all_ws=0.0, kits=0.0,
                  wood_short=0.0, metal_wanted=0.0, metal_made=0.0)
    for t in range(seasons):
        x = season_pos(t, seasons)
        winter = t % 4 == 3
        n = pop * (1 + pop_growth) ** (t / 4)
        adults = n * p["adult_share"]
        ws = adults * p["ws_per_adult"]
        tool_ws_each = p["ws_per_adult"] * p["tool_share"]
        has_tin = "tin" in access and (cut_tin_at is None or t < cut_tin_at)
        known = ["T7"]
        if x >= AT["MET03"] and "copper" in access:
            known.append("T8")
        if x >= AT["MET04"] and "arsenic" in access and x >= AT["MIN04"]:
            known.append("T9")
        if x >= AT["MET05"] and has_tin:
            known.append("T10")
        best = known[-1]
        bellow = bellows and x >= AT["FIR09"]
        # 1) wear: every adult works with its kit; worn kits break
        held = sum(c[1] for c in cohorts)
        if held < adults:                       # new adults start with a stone kit
            for js in jobs:
                cohorts.append(["T7", (adults - held) / len(jobs), KIT["T7"]["dur"], js])
        rank = ["T7", "T8", "T9", "T10"].index
        for c in cohorts:
            if c[0] != "T7":
                # doc 23 §4.1 dormancy tail: a kit that cannot be replaced in kind is worked on to quality 0.5 (×1.4)
                tail = p["dormancy_tail"] if rank(c[0]) > rank(best) else 1.0
                c[2] -= p["ws_per_adult"] * c[3] / tail
        for c in [c for c in cohorts if c[0] != "T7" and c[2] <= 0]:
            c[0], c[2] = "T7", KIT["T7"]["dur"]
        # 2) the planner wants a best-tier kit for every adult not on it (adoption spreads over adopt_seasons)
        on_best = sum(c[1] for c in cohorts if rank(c[0]) >= rank(best))   # a better kit still working is kept
        adopt = 1.0
        if best != "T7":
            first = {"T8": AT["MET03"], "T9": max(AT["MET04"], AT["MIN04"]), "T10": AT["MET05"]}[best]
            adopt = min(1.0, (x - first) * seasons / p["adopt_seasons"] + 1 / p["adopt_seasons"])
        want = max(0.0, adults * adopt - on_best) if best != "T7" else 0.0
        # 3) materials and labor per kit of the best tier
        k = KIT[best]
        rec = BELLOWS if bellow else KILN
        tool_tier = max((c[0] for c in cohorts if c[1] > 0.5), key=lambda s: ["T7", "T8", "T9", "T10"].index(s))
        chop = CHOP[tool_tier] * WOOD_PER_WS
        picks = PICKS[tool_tier]
        if best == "T10":
            cu, tin = k["metal"] * ALLOY["cu"], k["metal"] * ALLOY["tin"]
            ore = cu * rec["ore"]
            char = cu * rec["char"] + tin * TIN_SMELT["char"] + k["metal"] * ALLOY["char"]
            labor = (ore / (MINE["surface"] * picks) + tin * TIN_SMELT["ore"] / (MINE["placer"] * picks)
                     + cu * rec["ws"] + tin * TIN_SMELT["ws"] + k["metal"] * ALLOY["ws"])
        elif best == "T9":
            cu, tin = 0.0, 0.0
            ore = k["metal"] * rec["ore"]
            char = k["metal"] * rec["char"]
            labor = ore / (MINE["shaft"] * picks) + k["metal"] * rec["ws"]
        elif best == "T8":
            cu, tin = k["metal"], 0.0
            ore = cu * rec["ore"]
            char = cu * rec["char"]
            labor = ore / (MINE["surface"] * picks) + cu * rec["ws"]
        else:
            cu = tin = ore = char = labor = 0.0
        wood_kit = char * CHAR["wood"]
        labor += char * CHAR["ws"] + wood_kit / chop + CAST_WS
        # doc 23 §4.1 upgrade remelt: replaced copper/arsenical kits return 50% of their metal as copper
        frac = 0.0
        if cu > 0 and remelt_pool > 0 and best in ("T8", "T10"):
            frac = min(1.0, remelt_pool / max(1e-9, max(want, 1e-9) * cu))
            cu_char = cu * rec["char"]
            wood_kit -= frac * cu_char * CHAR["wood"]
            labor -= frac * (cu * rec["ore"] / (MINE["surface"] * picks) + cu * rec["ws"] + cu_char * CHAR["ws"]
                             + cu_char * CHAR["wood"] / chop)
        # arms: replacement over arms_life plus battle losses, once WAR-05
        arms = 0.0
        if warriors and x >= AT["WAR05"] and best in ("T9", "T10"):
            arms = n / 100 * p["warriors_per100"] * (1 / (4 * p["arms_life_years"]) + p["battle_loss"] / 4)
        # 4) wood: non-metal demand first (hearths, kilns, building), then charcoal
        hearths = max(1.0, n / p["hearth_per"])
        other_wood = hearths * (3.0 if winter else 1.5) + n * (p["kiln_wood"] + p["build_wood"]) / 4
        regrow = p["regrow"] * (2 if coppice else 1)
        dead = p["forest_tiles"] * p["deadwood"] / 4
        # charcoal is burned from standing stock only (doc 23 §7.4); deadwood feeds hearths and kilns first
        avail = sum(max(0.0, f - p["cleared_below"]) for f in forest) - max(0.0, other_wood - dead)
        # 5) how many kits this season: labor cap, wood
        budget = cap_share * ws
        arms_labor = arms * (ARMS["ws"] + ARMS["metal"] / k["metal"] * labor) if k["metal"] else 0.0
        arms_wood = arms * (ARMS["wood"] + ARMS["metal"] / k["metal"] * wood_kit) if k["metal"] else 0.0
        kits_labor = max(0.0, budget - arms_labor) / labor if labor else 0.0
        wood_left = max(0.0, avail - arms_wood)
        kits_wood = wood_left / wood_kit if wood_kit else 1e9
        made = min(want, kits_labor, kits_wood)
        totals["metal_wanted"] += want
        totals["metal_made"] += made
        if kits_wood < min(want, kits_labor):
            totals["wood_short"] += 1
        # 6) draw the wood from the forest (deadwood first, then standing stock, evenly)
        from_stock = max(0.0, other_wood - dead) + arms_wood + made * wood_kit
        remelt_pool = max(0.0, remelt_pool - made * cu * frac)
        stocks = [max(0.0, f - p["cleared_below"]) for f in forest]
        tot = sum(stocks)
        forest = [f - (from_stock * s / tot if tot > 0 else 0.0) for f, s in zip(forest, stocks)]
        forest = [min(p["forest_stock"], f + (regrow if f > p["cleared_below"] else regrow)) for f in forest]
        # 7) replace kits: the made kits go to adults on the lowest tiers first
        if made > 0:
            need = made
            for c in sorted(cohorts, key=lambda c: (["T7", "T8", "T9", "T10"].index(c[0]), c[2])):
                if need <= 0:
                    break
                if rank(c[0]) >= rank(best):
                    continue
                take = min(c[1], need)
                if c[0] in ("T8", "T9"):
                    remelt_pool += take * p["remelt"] * KIT[c[0]]["metal"]
                c[1] -= take
                need -= take
                cohorts.append([best, take, k["dur"], c[3]])
            merged = {}
            for c in cohorts:
                if c[1] > 1e-9:
                    key = (c[0], round(c[2], 6), c[3])
                    merged[key] = merged.get(key, 0.0) + c[1]
            cohorts = [[a, n_, b, j] for (a, b, j), n_ in merged.items()]
        used_ws = made * labor + arms_labor
        bronze = made * k["metal"] if best in ("T9", "T10") else 0.0
        bronze += arms * ARMS["metal"]
        totals["bronze"] += bronze
        totals["copper"] += made * cu + (arms * ARMS["metal"] * ALLOY["cu"] if best == "T10" else 0.0)
        totals["tin"] += made * tin + (arms * ARMS["metal"] * ALLOY["tin"] if best == "T10" else 0.0)
        totals["char"] += made * char + arms * ARMS["metal"] / k["metal"] * char if k["metal"] else 0.0
        totals["metal_wood"] += made * wood_kit + arms_wood
        totals["ws"] += used_ws
        totals["all_ws"] += ws
        totals["kits"] += made
        share6 = sum(c[1] for c in cohorts if c[0] in ("T9", "T10")) / adults
        share_t10 = sum(c[1] for c in cohorts if c[0] == "T10") / adults
        rows.append(dict(t=t, x=x, pop=n, best=best, made=made, want=want, used_ws=used_ws, metal_share=used_ws / ws,
                         forest=sum(forest) / (p["forest_tiles"] * p["forest_stock"]), share6=share6, share_t10=share_t10,
                         cleared=sum(1 for f in forest if f <= p["cleared_below"])))
    return rows, totals


def kit_costs(best, bellows=True, tool_tier=None):
    """Materials and labor for one kit of `best` with the given chain (22 §5.3), tools of `tool_tier` (default best)."""
    k = KIT[best]
    rec = BELLOWS if bellows else KILN
    tt = tool_tier or best
    chop, picks = CHOP[tt] * WOOD_PER_WS, PICKS[tt]
    cu = tin = ore = char = labor = 0.0
    if best == "T10":
        cu, tin = k["metal"] * ALLOY["cu"], k["metal"] * ALLOY["tin"]
        ore = cu * rec["ore"]
        char = cu * rec["char"] + tin * TIN_SMELT["char"] + k["metal"] * ALLOY["char"]
        labor = (ore / (MINE["surface"] * picks) + tin * TIN_SMELT["ore"] / (MINE["placer"] * picks)
                 + cu * rec["ws"] + tin * TIN_SMELT["ws"] + k["metal"] * ALLOY["ws"])
    elif best == "T9":
        ore = k["metal"] * rec["ore"]
        char = k["metal"] * rec["char"]
        labor = ore / (MINE["shaft"] * picks) + k["metal"] * rec["ws"]
    elif best == "T8":
        cu = k["metal"]
        ore = cu * rec["ore"]
        char = cu * rec["char"]
        labor = ore / (MINE["surface"] * picks) + cu * rec["ws"]
    wood = char * CHAR["wood"]
    labor += char * CHAR["ws"] + wood / chop + CAST_WS
    return dict(metal=k["metal"], cu=cu, tin=tin, ore=ore, char=char, wood=wood, ws=labor)


def steady_state(alloy="tin", bellows=True, people=100.0, arms=True, p=None):
    """22 §5.6 demand per `people` per year with the kit stock in equilibrium: kits = adults × tool work × 4 ÷ durability."""
    p = dict(P, **(p or {}))
    tier = {"tin": "T10", "arsenic": "T9", "copper": "T8"}[alloy]
    adults = people * p["adult_share"]
    ws_year = adults * p["ws_per_adult"] * 4
    kits = adults * p["ws_per_adult"] * p["tool_share"] * 4 / KIT[tier]["dur"]
    c = kit_costs(tier, bellows)
    arms_kits = (people / 100 * p["warriors_per100"] * (1 / p["arms_life_years"] + p["battle_loss"])) if arms and tier != "T8" else 0.0
    am = arms_kits * ARMS["metal"] / c["metal"]       # arms in kit-equivalents of metal
    out = dict(kits=kits, arms_kits=arms_kits, bronze=(kits + am) * c["metal"] if tier != "T8" else 0.0,
               copper=(kits + am) * c["cu"], tin=(kits + am) * c["tin"], char=(kits + am) * c["char"],
               metal_wood=(kits + am) * c["wood"] + arms_kits * ARMS["wood"],
               ws=(kits + am) * c["ws"] + arms_kits * ARMS["ws"], all_ws=ws_year)
    out["labor_share"] = out["ws"] / ws_year
    out["tools_only_bronze"] = kits * c["metal"] if tier != "T8" else 0.0
    return out


def coverage_after(alloy="tin", cap=0.10, seasons=52, pop=100.0):
    rows, _ = run(pop, seasons, ("tin", "copper") if alloy == "tin" else ("arsenic", "copper"), metal_cap=cap)
    first = AT["MET05"] if alloy == "tin" else max(AT["MET04"], AT["MIN04"])
    t0 = int(first * seasons)
    s12 = rows[min(len(rows) - 1, t0 + 12)]["share6"]
    reach70 = next((r["t"] - t0 for r in rows if r["t"] >= t0 and r["share6"] >= 0.70), None)
    return s12, reach70


def report(out=print):
    out("== T17 metal demand per 100 people per year (steady state, every adult equipped; 22 §5.6)")
    for alloy, bel in (("tin", False), ("tin", True), ("arsenic", True), ("copper", True)):
        d = steady_state(alloy, bel)
        out(f"  {alloy:8s} {'bellows' if bel else 'kiln   '}: bronze {d['bronze']:.2f} L (tools {d['tools_only_bronze']:.2f}; copper {d['copper']:.2f}, tin {d['tin']:.3f}),"
            f" charcoal {d['char']:.2f} L, wood for metal {d['metal_wood']:.1f} L, kits {d['kits']:.2f}, metal labor "
            f"{100 * d['labor_share']:.1f}% of work")
    out("  doc 22 T17: bronze 0.8–1.0 L; tin 0.08 L; charcoal 2.3–4.5 L; wood 20–26 L (14–19 bellows); labor 3–4%")

    out("\n== T18 bronze coverage: share of adults on rung 6 twelve seasons after MET-05 / MET-04 (access holds)")
    for cap in (0.05, 0.08, 0.10, 0.15):
        s12, r70 = coverage_after("tin", cap)
        a12, a70 = coverage_after("arsenic", cap)
        out(f"  metal labor cap {100 * cap:4.0f}%: tin {100 * s12:5.1f}% (70% after {r70} seasons);"
            f" arsenical {100 * a12:5.1f}% (70% after {a70})")

    out("\n== T19 fuel: a smelting settlement over a 13-minute E3 (52 seasons) and 8 seasons after")
    for pop in (40, 60, 80, 100):
        for cop in (False, True):
            rows, tot = run(pop, 60, ("tin", "copper"), coppice=cop)
            r52 = rows[51]
            want, made = tot["metal_wanted"], tot["metal_made"]
            ref, reft = run(pop, 60, ("tin", "copper"), coppice=cop, p=dict(forest_stock=1e6))
            loss = 1 - tot["kits"] / max(1e-9, reft["kits"])
            out(f"  {pop:3d} people, {'coppiced  ' if cop else 'no WOD-04 '}: standing wood {100 * (1 - r52['forest']):4.0f}% drawn down at KEY-4 "
                f"({r52['cleared']} of 13 tiles cleared); metal output lost to fuel {100 * loss:4.1f}%; rung 6 at KEY-4 {100 * r52['share6']:4.0f}%")
    out("  doc 22 T19: without WOD-04 30–50% drawn down by KEY-4; with it < 15%; metal output loss ≤ 30% with WOD-04")

    out("\n== Dormancy (22 §5.7): tin cut at KEY-4 after a full E3 of tin bronze; share of adults still on rung 6")
    rows, _ = run(100, 52 + 48, ("tin", "copper"), cut_tin_at=52)
    rows_g, _ = run(60, 52 + 48, ("tin", "copper"), cut_tin_at=52, pop_growth=0.06, coppice=True)
    for k in (0, 4, 8, 12, 20, 28, 36, 44):
        r = rows[52 + k - 1] if k else rows[51]
        g = rows_g[52 + k - 1] if k else rows_g[51]
        out(f"    +{k:2d} seasons: rung 6 {100 * r['share6']:5.1f}% (no growth), {100 * g['share6']:5.1f}% (60 people growing 6%/yr,"
            f" coppiced: new adults get copper)  best castable {r['best']}")
    life = {t: KIT[t]["dur"] / (P["ws_per_adult"] * P["tool_share"]) for t in ("T8", "T9", "T10")}
    out("  kit life at {:.2f} ws of tool work a season: T8 {:.0f}, T9 {:.0f}, T10 {:.0f} seasons".format(
        P["ws_per_adult"] * P["tool_share"], life["T8"], life["T9"], life["T10"]))

    out("\n== Sensitivity to a 20% error (100 people, tin, bellows unless stated)")
    base = steady_state("tin", True)
    for name, kw in (("durability ×0.8", dict(dur=0.8)), ("durability ×1.2", dict(dur=1.2)),
                     ("tool work share ×1.2", dict(p=dict(tool_share=0.72))), ("arms ×1.2", dict(p=dict(warriors_per100=4.8)))):
        if "dur" in kw:
            saved = {k: v["dur"] for k, v in KIT.items()}
            for k in KIT:
                KIT[k]["dur"] = saved[k] * kw["dur"]
            d = steady_state("tin", True)
            for k in KIT:
                KIT[k]["dur"] = saved[k]
        else:
            d = steady_state("tin", True, **kw)
        out(f"  {name:22s} bronze {d['bronze']:.2f} L ({100 * (d['bronze'] / base['bronze'] - 1):+.0f}%), wood {d['metal_wood']:.1f} L, "
            f"labor {100 * d['labor_share']:.1f}%")
    for name, pp in (("non-metal wood ×0.8", dict(kiln_wood=0.16, build_wood=0.12, hearth_per=43.75)),
                     ("non-metal wood ×1.2", dict(kiln_wood=0.24, build_wood=0.18, hearth_per=29.2)),
                     ("forest tiles ×0.8", dict(forest_tiles=10)), ("forest tiles ×1.2", dict(forest_tiles=16))):
        dd = []
        for pop in (60, 80, 100):
            rows, _ = run(pop, 52, ("tin", "copper"), p=pp)
            dd.append(f"{pop}: {100 * (1 - rows[51]['forest']):3.0f}%")
        out(f"  {name:22s} drawdown at KEY-4 without WOD-04  " + "  ".join(dd))

    out("\n== T20 tin access by KEY-4 (from territory_sim's E3 run): see territory_sim.py e3")
    out("\n== Sensitivity of E3 length to metal supply")
    out("  Insight is people-driven (02 §8.1); metal changes labor, arms and fuel, not headcount (FoodCap does not bind in")
    out("  villages, 07d §2). Know-how is 35% of Commons whatever is practised, so a civ without metal only re-routes")
    out("  practice from the smelt lineage: E3 length moves < 2% between 'no metal' and 'tin from KEY-3' (07d §5).")
    return True


def check():
    d = steady_state("tin", True)
    assert 0.55 <= d["bronze"] <= 1.0, d
    assert 0.02 <= d["labor_share"] <= 0.05, d
    s12, _ = coverage_after("tin", 0.10)
    assert s12 >= 0.70, s12
    rows, _ = run(100, 60, ("tin", "copper"), coppice=False)
    rows_c, _ = run(100, 60, ("tin", "copper"), coppice=True)
    assert rows[51]["forest"] < rows_c[51]["forest"], "coppicing saves wood"
    assert 1 - rows_c[51]["forest"] < 0.15, rows_c[51]["forest"]
    print("metal_sim check passed")


if __name__ == "__main__":
    if "--check" in sys.argv:
        check()
    else:
        report()
