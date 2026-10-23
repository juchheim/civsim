#!/usr/bin/env python3
"""One-time import of the markdown design documents into the YAML content tree (docs/MVP/16 §7, M2-M4).

    python3 import_md.py [--out ../../../content]

Reads 03a/03b/03c (catalog), 11 (E0-E2 numeric effects, practice, moments, flavor), 10 and 12 (registries),
14 (AI archetypes, difficulty) and balance/tools/params.py (tuned constants and era tables), and writes
content/. After this runs once, the YAML is the source of truth and this script is retired. Python standard
library only. Deviation from 16 §6.2 (documented there): this is a script, not a C# command.
"""
import importlib.util
import json
import os
import re
import sys
from collections import OrderedDict

HERE = os.path.dirname(os.path.abspath(__file__))
MVP = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(MVP))
OUT = os.path.join(REPO, "content")
if "--out" in sys.argv:
    OUT = os.path.abspath(sys.argv[sys.argv.index("--out") + 1])

ERA_BASE = [22, 50, 90, 180, 550, 3000, 15000, 130000, 750000, 3800000]
TYPE_MULT = {"D": 1.0, "M": 1.0, "S": 1.2, "R": 0.8, "I": 1.5, "W": 2.0, "K": 2.5}
THEME_BY_FILE = {"03a-research-craft.md": "craft", "03b-research-kin.md": "kin", "03c-research-reach.md": "reach"}
MINUS = "−"
errors = []


# ---- YAML writer (flow style for small records, block for projects) -----------------------------------
class Dec(str):
    """A decimal written exactly as given, so no float artifacts reach the YAML."""


_PLAIN = re.compile(r"^[A-Za-z_][A-Za-z0-9_ ./()-]*$")
_RESERVED = {"true", "false", "null", "yes", "no", "on", "off", "~", "y", "n"}


def scalar(v):
    if v is None:
        return "null"
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, Dec):
        return str(v)
    if isinstance(v, int):
        return str(v)
    if isinstance(v, float):
        raise TypeError("use Dec for decimals: %r" % v)
    s = str(v)
    if _PLAIN.match(s) and s.lower() not in _RESERVED and not s.endswith(" ") and ": " not in s and " #" not in s:
        return s
    return json.dumps(s, ensure_ascii=False)


def flow(v):
    if isinstance(v, dict):
        return "{" + ", ".join("%s: %s" % (k, flow(x)) for k, x in v.items() if x is not None) + "}"
    if isinstance(v, (list, tuple)):
        return "[" + ", ".join(flow(x) for x in v) + "]"
    return scalar(v)


def dec(x):
    """Format a python number as a Dec (shortest exact-looking decimal)."""
    if isinstance(x, int):
        return x
    t = repr(round(x, 9))
    return int(float(t)) if t.endswith(".0") else Dec(t)


def write(path, text):
    path = os.path.join(OUT, path)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


# ---- markdown parsing ---------------------------------------------------------------------------------
def cells(line):
    return [c.strip() for c in line.replace("\\|", "¦").strip().strip("|").split("|")]


def strip_md(s):
    s = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", s)
    s = s.replace("**", "").replace("`", "")
    return re.sub(r"\s+", " ", s).strip()


def read(name):
    with open(os.path.join(MVP, name), encoding="utf-8") as f:
        return f.read()


def parse_requires(cell):
    gates = [{"kind": k.lower(), "target": t.strip()} for k, t in re.findall(r"\[(Seen|Has|Near|Climate): ([^\]]+)\]", cell)]
    rest = re.sub(r"\[(?:Seen|Has|Near|Climate): [^\]]+\]", "", cell)
    anys = []
    for grp in re.findall(r"\(([^()]*¦[^()]*)\)", rest):
        anys += re.findall(r"[A-Z]{3}-\d+", grp)
    rest = re.sub(r"\([^()]*¦[^()]*\)", "", rest)
    req = re.findall(r"[A-Z]{3}-\d+", rest)
    return req, anys, gates


def parse_catalog():
    lineages, projects, keystones = OrderedDict(), OrderedDict(), OrderedDict()
    for fname, theme in THEME_BY_FILE.items():
        cur = None
        for line in read(fname).split("\n"):
            m = re.match(r"^## ([A-Z]{3}) — (.+)$", line)
            if m:
                cur = m.group(1)
                lineages[cur] = {"code": cur, "theme": theme, "name": m.group(2).strip(), "purpose": "", "anchors": ""}
                continue
            if cur and line.startswith("*Purpose:*"):
                lineages[cur]["purpose"] = strip_md(line[len("*Purpose:*"):])
            if cur and "*Anchors:*" in line:
                lineages[cur]["anchors"] = strip_md(line.split("*Anchors:*", 1)[1])
            if line.startswith("| KEY-"):
                c = cells(line)
                m2 = re.match(r"(\d+) × E(\d)", c[5])
                enters = 10 if c[2] == "Apex" else int(c[2][1:])
                era_left = 9 if c[2] == "Apex" else enters - 1
                req, anys, gates = parse_requires(c[4])
                keystones[c[0]] = {"id": c[0], "name": c[1], "era": era_left, "enters": enters, "type": "K",
                                   "requires": req, "requires_any": anys, "gates": gates,
                                   "breadth": {"era": int(m2.group(2)), "min": int(m2.group(1))},
                                   "prose": strip_md(c[6])}
                continue
            m3 = re.match(r"^\| ([A-Z]{3}-\d+) \|", line)
            if m3 and cur:
                c = cells(line)
                if len(c) != 6:
                    errors.append("bad catalog row %s" % c[0])
                    continue
                req, anys, gates = parse_requires(c[4])
                projects[c[0]] = {"id": c[0], "name": c[1], "lineage": cur, "theme": theme, "era": int(c[2][1:]),
                                  "type": c[3], "requires": req, "requires_any": anys, "gates": gates,
                                  "prose": strip_md(c[5])}
    return lineages, projects, keystones


def parse_doc11():
    rows, moments = {}, {}
    for line in read("11-e0-e2-project-specs.md").split("\n"):
        if not re.match(r"^\| ((?:[A-Z]{3}|KEY)-\d+) \|", line):
            continue
        c = cells(line)
        if len(c) == 7:
            rows[c[0]] = {"name": c[1], "type": c[2], "cost": int(c[3]), "requires": c[4], "effects": c[5], "practice": c[6]}
        elif len(c) == 3:
            moments[c[0]] = {"moment": strip_md(c[1]), "flavor": c[2].strip().strip('"“”')}
    return rows, moments


# ---- effect shorthand -> typed effects ----------------------------------------------------------------
STRUCT_IDS = {"hearth": "hearth", "lean-to": "lean_to", "hide tent": "hide_tent", "pit house": "pit_house",
              "wattle & daub house": "wattle_daub_house", "mudbrick house": "mudbrick_house", "longhouse": "longhouse",
              "storage pit": "storage_pit", "drying rack": "drying_rack", "granary": "granary", "kiln": "kiln",
              "quern": "quern", "thorn barrier": "thorn_barrier", "palisade": "palisade", "lookout post": "lookout_post",
              "seasonal camp": "seasonal_camp", "marker": "marker", "totem": "totem", "shrine": "shrine", "weir": "weir",
              "field": "field", "pasture": "pasture", "orchard": "orchard", "signal post": "signal_post"}
ROLE_IDS = {"elder", "shaman", "watcher", "collier", "smith", "midwife", "chief", "warrior"}
ACTION_IDS = {"keep fire": "keep_fire", "make fire": "make_fire", "bury": "bury", "gift exchange": "gift_exchange",
              "controlled burn": "controlled_burn", "gather clay": "gather_clay",
              "collect flint and ochre": "collect_flint_ochre", "fish": "fish", "tend the sick": "tend_sick",
              "barter with other civilizations": "barter", "collect native copper": "collect_native_copper",
              "draft oxen": "draft_oxen", "farm": "farm", "feast": "feast", "herd cattle and pigs": "herd",
              "hunt": "hunt", "herd": "herd", "raid": "raid", "mine flint pits": "mine_flint",
              "quarry stone blocks": "quarry", "raft crossing": "raft_crossing"}
METHOD_ACTIONS = {"chop", "forage", "scavenge", "hunt", "fish", "farm", "herd", "paddle", "shape timber", "clear"}
HAZARD_CAUSES = {"endemic", "epidemic", "starvation", "exposure", "injury", "predators", "violence", "child", "age 0", "maternal"}


def num(s):
    return Dec(s) if "." in s else int(s)


def slug(s):
    return re.sub(r"[^a-z0-9._]+", "_", s.lower()).strip("_")


def split_effects(text):
    text = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", text).replace("**", "")
    return [p.strip() for p in re.split(r" · (?![^()]*\))", text) if p.strip()]


def parse_mod(args, note):
    a = args.replace(MINUS, "-")
    pats = [(r"(\d+(?:\.\d+)?)% → (\d+(?:\.\d+)?)%", "set_pct"), (r"×[\d.]+ → ×([\d.]+)", "set"),
            (r"[\d.]+ → ([\d.]+)", "set"), (r"([+-])(\d+(?:\.\d+)?)%", "pct"), (r"×(\d+(?:\.\d+)?)", "mult"),
            (r"([+-])(\d+(?:\.\d+)?)(?!\d|%)", "flat")]
    for pat, op in pats:
        m = re.search(pat, a)
        if not m:
            continue
        stat = slug(re.sub(r"[,:]+\s*$", "", a[:m.start()]))
        if not stat:
            break
        if op == "set_pct":
            return {"kind": "mod", "stat": stat, "op": "set", "value": Dec(str(round(float(m.group(2)) / 100, 4))), "note": note}
        if op == "set":
            return {"kind": "mod", "stat": stat, "op": "set", "value": num(m.group(1)), "note": note}
        if op == "pct":
            v = round(float(m.group(2)) / 100, 4)
            return {"kind": "mod", "stat": stat, "op": "pct", "value": Dec(("-" if m.group(1) == "-" else "") + repr(v)), "note": note}
        if op == "mult":
            return {"kind": "mod", "stat": stat, "op": "mult", "value": num(m.group(1)), "note": note}
        return {"kind": "mod", "stat": stat, "op": "flat", "value": num(("-" if m.group(1) == "-" else "") + m.group(2)), "note": note}
    return {"kind": "rule", "text": note}


def parse_method(pid, args):
    overrides = {"WOD-03": {"kind": "method", "action": "shape_timber", "rung": 1, "name": "Adze", "mult": 1, "note": "adze"},
                 "AGR-04": {"kind": "method", "action": "clear", "rung": 1, "name": "Slash-and-burn", "mult": 1,
                            "note": "forest 1 ws/tile, fields shift every 4 yrs"}}
    if pid in overrides:
        return overrides[pid]
    m = re.match(r"^([A-Za-z ]+?), r(\d+)(\+?) (.*)$", args)
    if not m:
        return {"kind": "rule", "text": args}
    action = m.group(1).strip().lower()
    rung = int(m.group(2)) + (1 if m.group(3) else 0)
    rest = m.group(4)
    name = re.split(r"[,:]", rest, maxsplit=1)[0].strip()
    note = rest[len(name):].lstrip(",: ").strip()
    out = OrderedDict(kind="method", action=slug(action), rung=rung, name=name)
    mult = re.search(r"×(\d+(?:\.\d+)?)", rest)
    rate = re.search(r"(\d+(?:\.\d+)?) (R/ws|R/tile/yr)", rest)
    if rate:
        out["rate"] = num(rate.group(1))
        out["unit"] = rate.group(2)
    elif mult:
        out["mult"] = num(mult.group(1))
    else:
        out["mult"] = 1
    for pat in (r"\u00d7\d+(?:\.\d+)?", r"\d+(?:\.\d+)? R/(?:ws|tile/yr)"):
        note = re.sub(pat, "", note)
    note = re.sub(r"\s+", " ", note).strip(" ,;:")
    note = re.sub(r"\s+;", ";", note)
    if note:
        out["note"] = note
    return out


def parse_effect(pid, part):
    m = re.match(r"^([A-Za-z+]+)\((.*)\)\s*$", part)
    if not m:
        return [{"kind": "rule", "text": part}]
    head, args = m.group(1), m.group(2).strip()
    low = head.lower()
    if head == "Method":
        return [parse_method(pid, args)]
    if head == "Mod":
        if pid == "FIR-05":
            return [OrderedDict(kind="mod", stat="forage_on_burned_tile", op="pct", value=Dec("0.4"), note="for 3 yrs"),
                    OrderedDict(kind="mod", stat="game_on_burned_tile", op="pct", value=Dec("0.3"), note="for 3 yrs")]
        return [parse_mod(args, args)]
    if head == "Cap":
        m2 = re.match(r"^(Food|Lean|Housing|Group), (?:×(\d+(?:\.\d+)?)(?:\^(\d+(?:\.\d+)?))?|(\d[\d,]*)(.*))$", args)
        if not m2:
            return [{"kind": "rule", "text": part}]
        d = OrderedDict(kind="cap", cap=m2.group(1).lower())
        if m2.group(2):
            d["mult"] = num(m2.group(2))
            if m2.group(3):
                d["take"] = num(m2.group(3))
        else:
            d["value"] = int(m2.group(4).replace(",", ""))
            if m2.group(5).strip():
                d["note"] = m2.group(5).strip(" ,")
        return [d]
    if head == "Hazard":
        m2 = re.match(r"^(.+?), ×(\d+(?:\.\d+)?)(?::? ?(.*))?$", args)
        cause, scope = m2.group(1), None
        if cause.startswith("injury of "):
            cause, scope = "injury", cause[len("injury of "):]
        if cause not in HAZARD_CAUSES:
            errors.append("%s: unknown hazard cause %r" % (pid, cause))
        d = OrderedDict(kind="hazard", cause=cause.replace(" ", ""), mult=num(m2.group(2)))
        if scope:
            d["scope"] = scope
        if m2.group(3):
            d["note"] = m2.group(3)
        return [d]
    if head == "Good":
        out, rest = [], args
        while True:
            m2 = re.match(r"^(G-[A-Z0-9]+)(?:,\s*|:\s*|\s+|$)(.*)$", rest)
            if not m2:
                break
            out.append(OrderedDict(kind="good", id=m2.group(1)))
            rest = m2.group(2)
        if not out:
            if args.startswith("club"):
                return [OrderedDict(kind="good", id="G-CLUB")]
            if args.startswith("Dogs"):
                return [OrderedDict(kind="good", id="G-DOG", note=args)]
            return [{"kind": "rule", "text": args}]
        if rest.strip():
            out[-1]["note"] = rest.strip()
        return out
    if head == "Build":
        name = re.split(r"[:,]", args, maxsplit=1)[0].strip()
        sid = STRUCT_IDS.get(name.lower())
        if not sid:
            errors.append("%s: unknown structure %r" % (pid, name))
            return [{"kind": "rule", "text": part}]
        d = OrderedDict(kind="build", structure=sid)
        note = args[len(name):].lstrip(":, ").strip()
        if note:
            d["note"] = note
        return [d]
    if head == "Wonder":
        name = args.split(":", 1)[0].strip().lower()
        return [OrderedDict(kind="wonder", structure=name, note=args.split(":", 1)[1].strip() if ":" in args else None)]
    if head == "Role":
        m2 = re.match(r"^([A-Za-z]+)(?:, Insight (\d+)/yr)?(?:, (.*))?$", args)
        rid = m2.group(1).lower()
        if rid not in ROLE_IDS:
            errors.append("%s: unknown role %r" % (pid, rid))
        d = OrderedDict(kind="role", id=rid)
        if m2.group(2):
            d["insight"] = int(m2.group(2))
        if m2.group(3):
            d["note"] = m2.group(3)
        return [d]
    if head == "Action+":
        key = args.lower()
        aid, note = None, None
        for k in sorted(ACTION_IDS, key=len, reverse=True):
            if key == k or key.startswith(k + ",") or key.startswith(k + ":") or key.startswith(k + " "):
                aid, note = ACTION_IDS[k], args[len(k):].lstrip(",: ").strip() or None
                break
        if aid is None:
            errors.append("%s: unknown action %r" % (pid, args))
            return [{"kind": "rule", "text": part}]
        return [OrderedDict(kind="unlock_action", action=aid, note=note)]
    if head == "Cohesion":
        m2 = re.match(r"^([+-]?\d+)(.*)$", args.replace(MINUS, "-"))
        return [OrderedDict(kind="cohesion", value=int(m2.group(1)), note=m2.group(2).strip(" ,") or None)]
    if head == "Adopt":
        m2 = re.match(r"^p_teach, ×(\d+(?:\.\d+)?)$", args)
        return [OrderedDict(kind="adopt", mult=num(m2.group(1)))]
    if head == "EraAdvance":
        return [OrderedDict(kind="era_advance", era=int(args.lstrip("E")))]
    if head == "Tier":
        return [OrderedDict(kind="tier", tier=args.lower())]
    if head == "Unlock":
        d = OrderedDict(kind="unlock")
        m2 = re.match(r"^([A-Z]{3}-\d+)\s*(.*)$", args)
        if m2:
            d["project"] = m2.group(1)
            d["text"] = m2.group(2) or args
        else:
            d["text"] = args
        return [d]
    return [OrderedDict(kind={"knowhow": "know_how"}.get(low, low), text=args)]


# Corrections found by the first validation run (docs/MVP/16 §6.1). They fix gaps in the prose docs, not in the importer.
PRACTICE_FIX = {"CHM-03": "tend fire, craft", "COM-01": "tell stories, scout", "COM-03": "mark, tell stories",
                "COM-04": "guard, tell stories", "NRG-01": "herd, work_animals", "STN-10": "mine, build, ritual"}
METHOD_FIX = {"FOR-06": {"mult": Dec("2.4"), "note": "stand regrowth x2"},
              "SEA-03": {"mult": 2, "note": "coastal water within 2 tiles; 4 L"},
              "SEA-04": {"mult": Dec("2.5"), "note": "open-sea hops of up to 3 tiles"}}
EFFECT_ADD = {  # goods the registry says a project unlocks, and structures the project text implies
    "FOR-02": [OrderedDict(kind="good", id="G-ROOT")], "FOR-03": [OrderedDict(kind="good", id="G-NUTS")],
    "FOR-04": [OrderedDict(kind="good", id="G-WGRAIN"), OrderedDict(kind="good", id="G-SICKLE")],
    "AGR-02": [OrderedDict(kind="good", id="G-GRAIN")], "HNT-02": [OrderedDict(kind="good", id="G-SPEAR")],
    "HNT-05": [OrderedDict(kind="good", id="G-ATLATL")], "HNT-06": [OrderedDict(kind="good", id="G-BOW")],
    "HNT-07": [OrderedDict(kind="good", id="G-FISH"), OrderedDict(kind="good", id="G-HARPOON")],
    "HNT-08": [OrderedDict(kind="good", id="G-FISHKIT")], "FIB-01": [OrderedDict(kind="good", id="G-FIBER")],
    "CER-01": [OrderedDict(kind="good", id="G-CLAY")], "STN-08": [OrderedDict(kind="good", id="G-TOOL7")],
    "FIB-07": [OrderedDict(kind="good", id="G-GARMENT")], "BLD-02": [OrderedDict(kind="good", id="G-TENT")],
    "WAY-02": [OrderedDict(kind="build", structure="seasonal_camp", note="2 per settlement founded in E1-E3, radius-2 claim at 50% strength (07b 9.4)")],
    "HUS-03": [OrderedDict(kind="build", structure="pasture", note="holds the herd; a pasture tile carries 6 goats or sheep")],
}
ACK = {"HNT-05": (["V11"], "The atlatl is slower per hour than a cooperative drive but safer and needs fewer hunters (10 §4.2)."),
       "KEY-1": (["V9"], "Two free picks are accepted so the first keystone lands near the 7.5 minute onboarding target (07 F9, 08 §12).")}


def parse_practice(cell):
    toks = [slug(t.replace("/", "_")) for t in cell.split(",") if t.strip() and t.strip() != "—"]
    if not toks:
        return []
    base, extra = divmod(100, len(toks))
    return [OrderedDict(action=t, share=base + (extra if i == 0 else 0)) for i, t in enumerate(toks)]


# ---- registries ---------------------------------------------------------------------------------------
def table_after(doc, heading_regex):
    lines = read(doc).split("\n")
    i = next(n for n, l in enumerate(lines) if re.match(heading_regex, l))
    j = i + 1
    while not lines[j].startswith("|"):
        j += 1
    hdr = cells(lines[j])
    rows = []
    j += 2
    while j < len(lines) and lines[j].startswith("|"):
        rows.append(cells(lines[j]))
        j += 1
    return hdr, rows


def maybe_num(s):
    t = s.replace("**", "").replace(",", "").strip()
    m = re.match(r"^(-?\d+(?:\.\d+)?)", t)
    if m and (len(t) == len(m.group(1)) or t[len(m.group(1))] in " (%×/"):
        return num(m.group(1))
    return None


def goods_yaml():
    S = lambda *a: a  # noqa: E731
    g = [  # id, name, category, first_from, spoilage per season, durability, load, per, barter, note
        ("G-MEAT", "Meat (fresh)", "food", ["start"], "0.6", None, "0.5", None, "1"),
        ("G-PLANT", "Plant food (fresh)", "food", ["start"], "0.4", None, "0.5", None, "0.8"),
        ("G-ROOT", "Roots & tubers", "food", ["FOR-02"], "0.15", None, "0.5", None, "0.8"),
        ("G-NUTS", "Nuts & seeds", "food", ["FOR-03"], "0.05", None, "0.5", None, "1.2"),
        ("G-WGRAIN", "Wild grain", "food", ["FOR-04"], "0.08", None, "0.4", None, "1.2"),
        ("G-GRAIN", "Grain (domestic)", "food", ["AGR-02"], "0.05", "10% more to pests outside a granary", "0.4", None, "1.2"),
        ("G-FISH", "Fish (fresh)", "food", ["HNT-07"], "0.7", None, "0.5", None, "1"),
        ("G-DRIED", "Dried / smoked meat & fish", "food", ["FOD-02"], "0.1", None, "0.3", None, "1.5"),
        ("G-BREW", "Beer & bread", "food_feast", ["FOD-05"], "0.15", None, "0.5", None, "2"),
        ("G-WOOD", "Wood / fuel", "material", ["start"], None, "durable", "1", None, "0.3"),
        ("G-STONE", "Stone (knapping)", "material", ["start"], None, "durable", "1", None, "0.2"),
        ("G-FLINT", "Flint / chert", "material", ["MIN-01"], None, "durable", "1", None, "1"),
        ("G-OCHRE", "Ochre", "material", ["MIN-01"], None, "durable", "1", None, "2"),
        ("G-FIBER", "Plant fiber", "material", ["FIB-01"], "0.1", None, "1", None, "0.3"),
        ("G-HIDE", "Hide (raw)", "material", ["start"], "0.3", None, "0.5", None, "0.6"),
        ("G-BONE", "Bone & antler", "material", ["start"], None, "durable", "1", None, "0.3"),
        ("G-CORD", "Cord", "processed", ["FIB-01"], None, "durable", "0.1", 10, "0.5"),
        ("G-RAWHIDE", "Rawhide", "processed", ["FIB-02"], "0.05", None, "0.5", None, "1"),
        ("G-LEATHER", "Leather", "processed", ["FIB-03"], None, "durable", "0.5", None, "2"),
        ("G-CLAY", "Clay", "material", ["CER-01", "BLD-05"], None, "durable", "1", None, "0.2"),
        ("G-BRICK", "Mudbrick", "processed", ["BLD-05"], None, "durable", "0.1", 10, "0.2"),
        ("G-YARN", "Yarn", "processed", ["FIB-06"], None, "durable", "0.2", None, "1"),
        ("G-CLOTH", "Cloth", "processed", ["FIB-07"], None, "durable", "0.2", None, "4"),
        ("G-LIME", "Lime", "processed", ["CHM-03"], None, "durable", "1", None, "1.5"),
        ("G-PIGMENT", "Pigment", "processed", ["CHM-01"], None, "durable", "0.1", None, "2"),
        ("G-TAR", "Birch tar", "processed", ["CHM-02"], None, "durable", "0.1", None, "1.5"),
        ("G-CHAR", "Charcoal", "fuel", ["FIR-08"], None, "durable", "1", None, "1.5"),
        ("G-COPPER", "Copper", "metal", ["MET-02"], None, "durable", "1", None, "20"),
        ("G-TOOL1", "Pebble choppers & flakes (T1)", "tool", ["STN-01"], None, "1.0 ws", "0.3", None, "1"),
        ("G-TOOL2", "Hand axe kit (T2)", "tool", ["STN-02"], None, "2.0 ws", "0.3", None, "1.5"),
        ("G-TOOL3", "Prepared-core kit (T3)", "tool", ["STN-03"], None, "2.0 ws", "0.3", None, "2"),
        ("G-TOOL4", "Hafted kit (T4)", "tool", ["STN-04"], None, "3.0 ws", "0.3", None, "3"),
        ("G-TOOL5", "Blade kit (T5)", "tool", ["STN-05"], None, "4.5 ws", "0.3", None, "4"),
        ("G-TOOL6", "Composite microlith kit (T6)", "tool", ["STN-06"], None, "4.0 ws", "0.3", None, "5"),
        ("G-TOOL7", "Polished axe (T7)", "tool", ["STN-08"], None, "10 ws", "0.3", None, "8"),
        ("G-SPEAR", "Spear", "tool", ["HNT-02"], None, "5 ws", "0.3", None, "2"),
        ("G-ATLATL", "Spear-thrower and darts", "tool", ["HNT-05"], None, "8 ws", "0.3", None, "3"),
        ("G-BOW", "Bow and arrows", "tool", ["HNT-06"], None, "10 ws", "0.3", None, "4"),
        ("G-HARPOON", "Harpoon", "tool", ["HNT-07"], None, "6 ws", "0.3", None, "2"),
        ("G-FISHKIT", "Hooks and lines", "tool", ["HNT-08"], None, "8 ws", "0.3", None, "2"),
        ("G-NET", "Net", "tool", ["FIB-05"], None, "12 ws", "0.3", None, "4"),
        ("G-SICKLE", "Microlith sickle", "tool", ["FOR-04"], None, "6 ws", "0.2", None, "2"),
        ("G-STICK", "Digging stick", "tool", ["WOD-01"], None, "4 ws", "0.2", None, "0.5"),
        ("G-CLUB", "Wooden club", "tool", ["WOD-01"], None, "5 ws", "0.3", None, "0.5"),
        ("G-NEEDLE", "Bone needle set", "tool", ["STN-07"], None, "6 ws", "0.1", None, "3"),
        ("G-BASKET", "Basket", "container", ["FIB-05"], None, "8 ws", "0.3", None, "1"),
        ("G-POT", "Pot", "container", ["CER-02"], None, "10% breakage per move", "0.3", None, "1.5"),
        ("G-JAR", "Storage jar", "container", ["CER-03"], None, "10% breakage per move", "0.3", None, "2"),
        ("G-WRAP", "Hide wrap", "clothing", ["FIB-02"], None, "8 seasons", "0.3", None, "2"),
        ("G-TAILORED", "Tailored clothing", "clothing", ["FIB-04"], None, "12 seasons", "0.3", None, "6"),
        ("G-GARMENT", "Cloth garment", "clothing", ["FIB-07"], None, "16 seasons", "0.3", None, "6"),
        ("G-BEADS", "Beads", "luxury", ["ART-01"], None, "durable", "0.05", None, "5"),
        ("G-FIGURINE", "Figurine", "luxury", ["CER-01"], None, "durable", "0.1", None, "4"),
        ("G-CARVING", "Carving", "luxury", ["WOD-02"], None, "durable", "0.1", None, "4"),
        ("G-POLAXE", "Polished axe (prestige)", "luxury", ["STN-08"], None, "10 ws", "0.4", None, "12"),
        ("G-COPORN", "Copper ornament", "luxury", ["MET-01"], None, "durable", "0.05", None, "25"),
        ("G-TENT", "Tent kit", "shelter", ["BLD-02"], None, "30 seasons", "1", None, "6"),
        ("G-DOG", "Dog", "animal", ["HUS-01"], None, "eats 0.3 R/season", "0", None, "3"),
    ]
    lines = ["# Goods registry (docs/MVP/10 §10). Imported once by catalog-tools/import_md.py; edit here from now on.",
             "schema: 1", "goods:"]
    for gid, name, cat, ff, sp, dur, load, per, barter in g:
        d = OrderedDict(id=gid, name=name, category=cat, first_from=ff)
        if sp:
            d["spoilage"] = Dec(sp)
        if dur:
            d["durability"] = dur
        d["load"] = num(load)
        if per:
            d["per"] = per
        d["barter"] = num(barter)
        lines.append("  - " + flow(d))
    return "\n".join(lines) + "\n", {x[0] for x in g}


def structures_yaml():
    hdr, rows = table_after("10-e0-e2-actions-and-goods.md", r"^## 6\. Build")
    lines = ["# Structures registry (docs/MVP/10 §6). Imported once; edit here from now on.", "schema: 1", "structures:"]
    ids = set()
    for r in rows:
        name = strip_md(r[0])
        low = name.lower().replace(" (wonder)", "")
        key = {"grinding stone / quern": "quern", "marker (cairn or totem)": "marker"}.get(low, low)
        sid = STRUCT_IDS.get(key, key.replace("megalith", "megalith"))
        ids.add(sid)
        d = OrderedDict(id=sid, name=name)
        d["class"] = "wonder" if "Wonder" in r[0] else "structure"
        d["unlocked_by"] = r[1].split(" ")[0]
        labor = maybe_num(r[2])
        if labor is not None and re.match(r"^[\d.]+$", r[2]):
            d["labor"] = labor
        else:
            d["labor_note"] = strip_md(r[2])
        d["materials"] = strip_md(r[3])
        d["effect"] = strip_md(r[4])
        d["upkeep"] = strip_md(r[5])
        lines.append("  - " + flow(d))
    for sid, name, ub, note in [("totem", "Totem", "BEL-02", "Totem poles and marks: +Cohesion; +claim at totems (BEL-02)."),
                                ("signal_post", "Signal post", "COM-04", "Alerts cross the territory instantly: defense response time -70% (COM-04).")]:
        ids.add(sid)
        d = OrderedDict(id=sid, name=name)
        d["class"] = "structure"
        d["unlocked_by"] = ub
        d["effect"] = note
        lines.append("  - " + flow(d))
    return "\n".join(lines) + "\n", ids


def tools_yaml():
    hdr, rows = table_after("10-e0-e2-actions-and-goods.md", r"^### 5\.2 Knap")
    lines = ["# Tool tiers (docs/MVP/10 §5.2). Imported once; edit here from now on.", "schema: 1", "tools:"]
    names = ["t0", "t1", "t2", "t3", "t4", "t5", "t6", "t7", "bone_set", "copper_awls"]
    for tid, r in zip(names, rows):
        d = OrderedDict(id=tid, name=strip_md(r[1]), unlocked_by=re.findall(r"[A-Z]{3}-\d+", r[2]) or ["start"],
                        make_time=maybe_num(r[3]) if re.match(r"^\*{0,2}[\d.]", r[3]) else None,
                        inputs=strip_md(r[4]), durability=strip_md(r[5]), enables=strip_md(r[6]))
        lines.append("  - " + flow(d))
    return "\n".join(lines) + "\n"


def roles_yaml():
    roles = [("elder", "Elder", 8, "GOV-01"), ("shaman", "Shaman", 12, "BEL-03"), ("priest", "Priest", 16, "BEL-06"),
             ("scribe", "Scribe", 24, "LRN-04"), ("scholar", "Scholar", 48, "LRN-05"), ("researcher", "Researcher", 120, "LRN-12"),
             ("watcher", "Watcher", 0, "DEF-02"), ("collier", "Collier", 0, "FIR-08"), ("smith", "Smith", 0, "MET-02"),
             ("midwife", "Midwife", 0, "MED-03"), ("chief", "Chief", 0, "GOV-02"), ("warrior", "Warrior", 0, "WAR-04")]
    lines = ["# Roles (docs/MVP/02 §8.1 with the 07 §5 output scale). Insight per person per year at Standard pace.",
             "schema: 1", "roles:"]
    for rid, name, ins, ub in roles:
        lines.append("  - " + flow(OrderedDict(id=rid, name=name, insight=ins, unlocked_by=ub)))
    return "\n".join(lines) + "\n"


KNOWHOW = {  # practice action -> lineage split (docs/MVP/10 §11; craft is an M1 addition)
    "forage": {"FOR": 80, "FIB": 10, "INQ": 10}, "hunt": {"HNT": 70, "WAR": 15, "INQ": 15},
    "butcher": {"HNT": 70, "WAR": 15, "INQ": 15}, "fish": {"HNT": 70, "SEA": 30}, "farm": {"AGR": 85, "FOR": 15},
    "herd": {"HUS": 90, "TRN": 10}, "chop": {"WOD": 70, "STN": 30}, "knap": {"STN": 85, "MIN": 15},
    "fiber": {"FIB": 90, "CHM": 10}, "hide": {"FIB": 90, "CHM": 10}, "fiber_hide": {"FIB": 90, "CHM": 10},
    "clay": {"CER": 80, "FIR": 20}, "tend_fire": {"FIR": 50, "FOD": 50}, "cook": {"FIR": 50, "FOD": 50},
    "preserve": {"FOD": 100}, "build": {"BLD": 80, "MEC": 10, "DEF": 10}, "haul": {"TRN": 70, "WAY": 30},
    "paddle": {"SEA": 100}, "mine": {"MIN": 80, "STN": 20}, "smelt": {"MET": 70, "FIR": 30}, "heal": {"MED": 100},
    "teach": {"LRN": 40, "ART": 30, "COM": 30}, "tell_stories": {"LRN": 40, "ART": 30, "COM": 30},
    "ritual": {"BEL": 80, "ART": 20}, "feast": {"SOC": 70, "FOD": 30}, "socialize": {"SOC": 70, "FOD": 30},
    "council": {"GOV": 100}, "scout": {"WAY": 70, "TER": 20, "INQ": 10}, "mark": {"TER": 80, "DEF": 20},
    "trade": {"TRD": 70, "DIP": 30}, "meet_strangers": {"DIP": 70, "COM": 30}, "guard": {"WAR": 60, "DEF": 40},
    "fight": {"WAR": 60, "DEF": 40}, "count_stores": {"NUM": 100},
    "craft": {"STN": 25, "FIB": 15, "CER": 15, "ART": 20, "CHM": 15, "MET": 10},
    "work_animals": {"NRG": 60, "HUS": 40},
}
KNOWHOW_NOTES = {"craft": "Added in M1: 10 §11 has no generic craft row; split chosen so craft-based E0-E2 projects feed their own lineage.",
                 "work_animals": "Added in M1: no E0-E2 activity fed the NRG lineage (found by check V14).",
                 "chop": "From era 2 the split is WOD 85 / STN 15 (knowhow_from_era2)."}


def practice_yaml():
    lines = ["# Practice actions: which lineages' Know-how an activity feeds (docs/MVP/10 §11).", "schema: 1", "practice:"]
    for pid, split in KNOWHOW.items():
        d = OrderedDict(id=pid, knowhow=split)
        if pid == "chop":
            d["knowhow_from_era2"] = {"WOD": 85, "STN": 15}
        if pid in KNOWHOW_NOTES:
            d["note"] = KNOWHOW_NOTES[pid]
        lines.append("  - " + flow(d))
    return "\n".join(lines) + "\n"


def actions_yaml():
    fam = [("forage", "Forage", "family", "1.6", "R/ws"), ("scavenge", "Scavenge", "family", "0.9", "R/ws"),
           ("hunt", "Hunt", "family", None, None), ("fish", "Fish", "family", None, None), ("farm", "Farm", "family", None, None),
           ("clear", "Clear land", "family", None, None), ("herd", "Herd", "family", None, None),
           ("chop", "Chop wood", "family", "2.0", "L/ws"), ("shape_timber", "Shape timber", "family", None, None),
           ("paddle", "Paddle", "family", None, None),
           ("keep_fire", "Keep fire", "activity", None, None), ("make_fire", "Make fire", "activity", None, None),
           ("bury", "Bury the dead", "activity", None, None), ("raft_crossing", "Raft crossing", "activity", None, None),
           ("gift_exchange", "Gift exchange", "activity", None, None), ("controlled_burn", "Controlled burn", "activity", None, None),
           ("gather_clay", "Gather clay", "activity", "3", "L/ws"), ("collect_flint_ochre", "Collect flint and ochre", "activity", "1", "L/ws"),
           ("tend_sick", "Tend the sick", "activity", None, None), ("barter", "Barter", "activity", None, None),
           ("raid", "Raid", "activity", None, None), ("mine_flint", "Mine flint pits", "activity", "3", "L/ws"),
           ("quarry", "Quarry stone blocks", "activity", "2", "L/ws"), ("collect_native_copper", "Collect native copper", "activity", "0.2", "L/ws"),
           ("draft_oxen", "Draft oxen", "activity", None, None), ("feast", "Feast", "activity", None, None)]
    lines = ["# Actions: method-ladder families and one-off activities (docs/MVP/10 §3-§8).",
             "# Ladder rungs are NOT listed here: they are the `method` effects of projects, collected per action by the compiler.",
             "schema: 1", "actions:"]
    for aid, name, cls, rate, unit in fam:
        d = OrderedDict(id=aid, name=name)
        d["class"] = cls
        if rate:
            d["base_rate"] = Dec(rate) if "." in rate else int(rate)
            d["unit"] = unit
        lines.append("  - " + flow(d))
    return "\n".join(lines) + "\n"


def generic_yaml(title, key, doc, heading, id_col=0, skip=()):
    hdr, rows = table_after(doc, heading)
    lines = ["# %s" % title, "schema: 1", "%s:" % key]
    ids = []
    keys = [slug(h) for h in hdr]
    for r in rows:
        rid = slug(re.sub(r"\([^)]*\)|\*\*", "", r[id_col]).replace("/", " "))
        ids.append(rid)
        d = OrderedDict(id=rid, name=strip_md(r[id_col]))
        for k, cell in zip(keys, r):
            if k in skip or k == keys[id_col]:
                continue
            n = maybe_num(cell)
            d[k] = n if n is not None and re.match(r"^\*{0,2}-?[\d.,]+\*{0,2}$", cell.strip()) else strip_md(cell)
        lines.append("  - " + flow(d))
    return "\n".join(lines) + "\n", ids


def eras_yaml(params):
    names = ["Dawn", "Hunters", "Roots", "Bronze", "Iron", "Faith & Fief", "Discovery", "Industry", "Modern", "Information"]
    E = {k: v["tuned"] for k, v in params.ERA.items()}
    reach = [6, 9, 12, 15, 20, 25, 35, 50, 80, 160]
    sea = [0, 0, 1, 1, 2, 2, 2, 2, 2, 2]
    cm = ["1.0", "1.15", "1.3", "1.3", "1.3", "1.3", "1.3", "1.56", "1.56", "1.56"]
    claim_t = [3, 3, 4, 4, 5, 5, 5, 7, 9, 9]
    assert E["EraBase"] == ERA_BASE, "EraBase differs from params.py tuned"
    lines = ["# Eras (docs/MVP/01 §8, 07 §5, 07b, 09 §11.1). Tuned values from balance/tools/params.py at import.",
             "schema: 1", "eras:"]
    for i in range(10):
        d = OrderedDict(id=i, name=names[i], base_cost=E["EraBase"][i], breadth=E["BreadthN"][i], rep_scale=E["RepScale"][i],
                        claim_radius=claim_t[i], reach=reach[i], sea_level=sea[i], claim_mult=Dec(cm[i]) if cm[i] != "1.0" else 1,
                        winter_mult=dec(E["WinterMult"][i]), fert_mult=dec(E["FertMult"][i]),
                        mort_child=dec(E["MortChild"][i]), mort_adult=dec(E["MortAdult"][i]), mort_elder=dec(E["MortElder"][i]),
                        cost_mod=dec(E["CostMod"][i]))
        lines.append("  - " + flow(d))
    return "\n".join(lines) + "\n"


def balance_yaml(params):
    lines = ["# Tuned constants (docs/MVP/07, 07b, 09, 13). Values from balance/tools/params.py (tuned) plus the rules below.",
             "schema: 1", "type_mult: " + flow(OrderedDict((k, dec(v)) for k, v in TYPE_MULT.items())), "constants:"]
    for k, v in params.SCALARS.items():
        val, unit, note = v[1], v[2], v[3]
        lines.append("  - " + flow(OrderedDict(id=k, value=dec(val) if not isinstance(val, int) else val, unit=unit, source=note)))
    extra = [("OwnershipThreshold", "0.7", "claim", "07b R3"), ("OwnershipLead", "0.2", "share", "05 §11.2"),
             ("FlipSeasons", 1, "seasons", "05 §11.2"), ("FalloffTail", 1, "tiles", "07b R3c: max(0, 1 - d/(r+1))"),
             ("CampsPerSettlement", 2, "camps", "07b §9.4 (WAY-02)"), ("CampRadius", 2, "tiles", "07b §9.4"),
             ("CampEmit", "0.5", "share", "07b §9.4"), ("TickRate", 10, "ticks/s", "05 §3.1"),
             ("SeasonTicks", 150, "ticks", "05 §3.1"), ("ChecksumInterval", 50, "ticks", "15 §6 D8"),
             ("BandStart", 12, "people", "05 §20"), ("DiffusionStep", "0.1", "share per contacted civ", "02 §8.3"),
             ("DiffusionFloor", "0.5", "share", "02 §8.3")]
    for k, v, unit, src in extra:
        lines.append("  - " + flow(OrderedDict(id=k, value=Dec(v) if isinstance(v, str) else v, unit=unit, source=src)))
    return "\n".join(lines) + "\n"


def ai_yaml():
    arch = [("builder", "Builder", [0.5, 0.25, 0.25], "high", "moderate", "trade-friendly", "Tool tiers, wonders, efficient core"),
            ("grower", "Grower", [0.2, 0.6, 0.2], "high", "fast fission", "friendly", "Many settlements, big population"),
            ("wanderer", "Wanderer", [0.2, 0.2, 0.6], "medium", "widest claims, overseas early", "first to contact, gifts", "Maps, boats, trade routes"),
            ("warlord", "Warlord", [0.3, 0.2, 0.5], "low", "opportunistic", "alliances of convenience", "Raids, conquest windows"),
            ("sage", "Sage", [0.25, 0.5, 0.25], "high", "slow early, surges", "friendly, tolerant", "Insight, Firsts, institutions")]
    a = ["# AI archetypes (docs/MVP/14 §9.1). Style, never competence.", "schema: 1", "archetypes:"]
    for i, n, lean, raid, exp, dip, sig in arch:
        a.append("  - " + flow(OrderedDict(id=i, name=n, leaning=OrderedDict(craft=dec(lean[0]), kin=dec(lean[1]), reach=dec(lean[2])),
                                           raid_margin=raid, expansion=exp, diplomacy=dip, signature=sig)))
    diff = [("settler", "Settler", 1, 2, "none", "yearly", 20, "reacts to threats 2 seasons late"),
            ("chieftain", "Chieftain", 2, 4, "archetype_posterior", "season", 5, None),
            ("warlord", "Warlord", 3, 8, "full", "season_and_events", 0, None),
            ("legend", "Legend", 4, 16, "full_coalition", "season_and_events", 0, None)]
    d = ["# AI difficulty levels (docs/MVP/14 §9.2). Better thinking, never bonuses.", "schema: 1", "difficulty:"]
    for i, n, hor, beam, model, replan, prior, note in diff:
        d.append("  - " + flow(OrderedDict(id=i, name=n, horizon_eras=hor, beam_width=beam, opponent_model=model, replan=replan,
                                           council_prior_pct=prior, note=note)))
    return "\n".join(a) + "\n", "\n".join(d) + "\n"


OPENINGS = {  # docs/MVP/04 §7: the three validated example openings (ids in research order, KEY-n completes an era)
    "grower": ("Grower (Kin-leaning)", "FOR-01 SOC-01 FIR-01 FOD-01 SOC-02 STN-01 WOD-01 FOR-02 FIB-01 COM-01 COM-02 ART-01 ART-02 STN-02 FIR-02 FIR-03 STN-03 STN-04 WAY-01 KEY-1 "
               "SOC-03 WAY-02 FIB-05 FOR-03 STN-05 FOD-02 STN-06 FOR-04 FOD-03 MED-01 FOR-05 FOR-06 FIB-02 BLD-01 WOD-02 BLD-02 BLD-03 LRN-01 LRN-02 KEY-2"),
    "builder": ("Builder (Craft-leaning)", "STN-01 FIR-01 FOR-01 STN-02 WOD-01 FIB-01 FIR-02 STN-03 FIR-03 FIR-04 BLD-01 STN-04 FIB-02 COM-01 COM-02 SOC-01 KEY-1 "
                "STN-05 FIR-06 STN-06 CHM-01 CHM-02 ART-01 WOD-02 MEC-01 BLD-02 BLD-03 FIB-03 FIB-05 MIN-01 FOR-02 WAY-01 WAY-02 FOR-03 FOR-04 FOD-01 FOD-02 FOD-03 KEY-2"),
    "wanderer": ("Wanderer (Reach-leaning)", "WAY-01 COM-01 HNT-01 TER-01 DIP-01 COM-02 STN-01 FIR-01 FOR-01 SOC-01 STN-02 FIB-01 FIR-02 STN-03 STN-04 KEY-1 "
                 "WOD-01 FOR-02 WAY-02 TER-02 INQ-02 WAY-03 SEA-01 HNT-02 HNT-03 INQ-01 HNT-04 HUS-01 TRN-01 TRN-02 SOC-02 SOC-03 DIP-02 FIB-05 FOR-03 STN-05 STN-06 FOR-04 FIR-03 FOD-01 FOD-02 FOD-03 FIB-02 BLD-01 BLD-02 ART-01 WOD-02 BLD-03 KEY-2"),
}


def openings_yaml():
    lines = ["# Validated example openings (docs/MVP/04 §7). The validator (V18) replays them against the catalog.", "schema: 1", "openings:"]
    for oid, (name, seq) in OPENINGS.items():
        lines.append("  - " + flow(OrderedDict(id=oid, name=name, steps=seq.split())))
    return "\n".join(lines) + "\n"


def guarantees_yaml():
    g = [("freshwater", "start", 2, "Freshwater"), ("clay", "start", 20, "Clay"), ("native_copper", "start", 20, "Native copper"),
         ("flint", "start", 15, "Flint"), ("wolves", "start", 20, "Wolves"), ("wild_goats_sheep", "start", 25, "Wild goats or sheep"),
         ("herds", "start", 12, "Herds"), ("grain_stands", "start", 10, "Wild grain"),
         ("gold_silver", "continental", None, "Gold or Silver"), ("coal", "continental", None, "Coal"),
         ("oil", "continental", None, "Oil seep"), ("tin", "continental", None, "Tin"), ("iron", "continental", None, "Iron"),
         ("salt", "continental", None, "Salt")]
    lines = ["# World-generation guarantees (docs/MVP/12 §10). `gate` names match `[Seen: ...]` gate targets.", "schema: 1", "guarantees:"]
    for gid, scope, radius, gate in g:
        lines.append("  - " + flow(OrderedDict(id=gid, scope=scope, radius=radius, gate=gate)))
    return "\n".join(lines) + "\n"


# ---- projects -----------------------------------------------------------------------------------------
def project_yaml(p, d11, mom):
    status = "numeric" if p["id"] in d11 else "draft"
    L = ["  - id: " + p["id"], "    name: " + scalar(p["name"]), "    era: %d" % p["era"], "    type: " + p["type"],
         "    status: " + status]
    L.append("    requires: " + flow(p["requires"]))
    if p["id"] in ACK:
        L.append("    ack: " + flow(ACK[p["id"]][0]))
        L.append("    ack_reason: " + scalar(ACK[p["id"]][1]))
    if p["requires_any"]:
        L.append("    requires_any: " + flow(p["requires_any"]))
    if p["gates"]:
        L.append("    gates: " + flow([OrderedDict(kind=g["kind"], target=g["target"]) for g in p["gates"]]))
    if status == "numeric":
        effects = []
        for part in split_effects(d11[p["id"]]["effects"]):
            effects += parse_effect(p["id"], part)
        for e in effects:
            if e.get("kind") == "method" and p["id"] in METHOD_FIX:
                e.update(METHOD_FIX[p["id"]])
        effects += EFFECT_ADD.get(p["id"], [])
        L.append("    effects:")
        for e in effects:
            L.append("      - " + flow(e))
        pr = parse_practice(PRACTICE_FIX.get(p["id"], d11[p["id"]]["practice"]))
        if pr:
            L.append("    practice: " + flow(pr))
        if p["id"] in mom:
            L.append("    flavor: " + scalar(mom[p["id"]]["flavor"]))
            L.append("    moment: " + scalar(mom[p["id"]]["moment"]))
    else:
        L.append("    effects:")
        L.append("      - " + flow(OrderedDict(kind="prose", text=p["prose"])))
    return "\n".join(L)


def main():
    spec = importlib.util.spec_from_file_location("params", os.path.join(MVP, "balance", "tools", "params.py"))
    params = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(params)

    lineages, projects, keystones = parse_catalog()
    d11, mom = parse_doc11()
    assert len(projects) == 509 and len(keystones) == 10, (len(projects), len(keystones))

    # consistency between the catalog tables and doc 11
    for pid, r in d11.items():
        src = projects.get(pid) or keystones.get(pid)
        if src is None:
            errors.append("doc 11 has unknown id %s" % pid)
            continue
        req, anys, gates = parse_requires(r["requires"])
        if src["name"] != r["name"] or src["type"] != r["type"]:
            errors.append("%s: name/type differ between catalog and doc 11" % pid)
        if sorted(req) != sorted(src["requires"]) or sorted(anys) != sorted(src["requires_any"]):
            errors.append("%s: requires differ between catalog and doc 11: %s vs %s" % (pid, req + anys, src["requires"] + src["requires_any"]))
        era = src["era"]
        want = int(ERA_BASE[era] * TYPE_MULT[src["type"]] + 0.5)
        if want != r["cost"]:
            errors.append("%s: doc 11 cost %d != EraBase x TypeMult = %d" % (pid, r["cost"], want))

    for code, lin in lineages.items():
        L = ["# %s: %s (%s). Imported once from docs/MVP/03*.md; edit here from now on." % (code, lin["name"], lin["theme"]),
             "schema: 1", "lineage: " + flow(OrderedDict(code=code, theme=lin["theme"], name=lin["name"], purpose=lin["purpose"],
                                                         anchors=lin["anchors"])), "projects:"]
        for p in [x for x in projects.values() if x["lineage"] == code]:
            L.append(project_yaml(p, d11, mom))
        write("catalog/%s-%s.yaml" % (code.lower(), slug(lin["name"]).replace("_", "-")), "\n".join(L) + "\n")

    K = ["# Era keystones KEY-1..KEY-10 (docs/MVP/03c). `era` is the era being left; `enters` the era entered (10 = Apex).",
         "schema: 1", "keystones:"]
    for k in keystones.values():
        K.append("  - id: " + k["id"])
        K.append("    name: " + scalar(k["name"]))
        K.append("    era: %d" % k["era"])
        K.append("    enters: %d" % k["enters"])
        K.append("    type: K")
        K.append("    status: " + ("numeric" if k["id"] in d11 else "draft"))
        K.append("    requires: " + flow(k["requires"]))
        if k["id"] in ACK:
            K.append("    ack: " + flow(ACK[k["id"]][0]))
            K.append("    ack_reason: " + scalar(ACK[k["id"]][1]))
        K.append("    breadth: " + flow(k["breadth"]))
        K.append("    effects:")
        if k["id"] in d11:
            for part in split_effects(d11[k["id"]]["effects"]):
                for e in parse_effect(k["id"], part):
                    K.append("      - " + flow(e))
            K.append("    flavor: " + scalar(mom[k["id"]]["flavor"]))
            K.append("    moment: " + scalar(mom[k["id"]]["moment"]))
        else:
            K.append("      - " + flow(OrderedDict(kind="prose", text=k["prose"])))
    write("catalog/keystones.yaml", "\n".join(K) + "\n")

    goods, _ = goods_yaml()
    write("registries/goods.yaml", goods)
    structs, _ = structures_yaml()
    write("registries/structures.yaml", structs)
    write("registries/tools.yaml", tools_yaml())
    write("registries/roles.yaml", roles_yaml())
    write("registries/practice.yaml", practice_yaml())
    write("registries/actions.yaml", actions_yaml())
    write("registries/eras.yaml", eras_yaml(params))
    write("registries/guarantees.yaml", guarantees_yaml())
    write("registries/openings.yaml", openings_yaml())
    arch, diff = ai_yaml()
    write("registries/archetypes.yaml", arch)
    write("registries/difficulty.yaml", diff)
    b, _ = generic_yaml("Biomes (docs/MVP/12 §4).", "biomes", "12-world-generation.md", r"^## 4\. Biomes")
    write("registries/biomes.yaml", b)
    f, _ = generic_yaml("Fauna (docs/MVP/12 §6).", "fauna", "12-world-generation.md", r"^## 6\. Fauna")
    write("registries/fauna.yaml", f)
    dp, _ = generic_yaml("Deposits (docs/MVP/12 §8).", "deposits", "12-world-generation.md", r"^## 8\. Deposits")
    write("registries/deposits.yaml", dp)
    write("constants/balance.yaml", balance_yaml(params))

    if errors:
        print("IMPORT PROBLEMS:")
        for e in errors:
            print("  ", e)
        return 1
    print("projects %d (numeric %d), keystones %d, lineages %d" % (len(projects), sum(1 for p in projects if p in d11), len(keystones), len(lineages)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
