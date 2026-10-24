"""E3 combat oracle (docs/MVP/22 §7, §15.2 item 4; findings in 07d §6).

A bit-exact Python port of the C# combat code (src/CivSim.Sim/Politics/Combat.cs, Raids.cs,
CombatTables.cs, with Fix64 Q32.32 arithmetic and the Pcg32/Hash64 streams of
src/CivSim.Sim/Randomness), extended with the Bronze Age rules of doc 22 §7:

  * weapons: copper 1.8, bronze 2.1 arsenical / 2.3 tin (2.2 table mean); training ×1.3
  * shields: casualties taken ×0.7 by a side whose fighters carry arms kits
  * chariots: crew ×2 strength on open ground, casualties taken ×0.5, none against walls
  * hillfort ×2.5 and stone walls ×3.0 (replace the palisade); loot ×0.25 behind either
  * the levy host and the investment (siege) model of §7.4

With every E3 extension off, `raid()` and `fight()` reproduce `Raids.Resolve` and `Combat.Fight`
draw for draw (the E2 table of 13 §12.1 comes out identical to `civsim-harness politics
--report-combat`; `--selftest` checks the published numbers).

    uv run --no-project --with numpy python combat_e3.py              # §7.5 tables, T21-T22, levy, siege
    uv run --no-project --with numpy python combat_e3.py --trials 1000
    uv run --no-project --with numpy python combat_e3.py --selftest   # port vs the C# E2 table, then assertions
"""
import sys

MASK64 = (1 << 64) - 1
MASK32 = (1 << 32) - 1
ONE = 1 << 32


# ---- Fix64 (Q32.32, raw ints) ---------------------------------------------------------
def fx(n, d=1):
    """Fix64.FromRatio(n, d): truncates toward zero."""
    q = abs(n << 32) // abs(d)
    return q if (n >= 0) == (d > 0) else -q


def fmul(a, b):
    p = a * b
    return p >> 32 if p >= 0 else -((-p) >> 32)


def fdiv(a, b):
    q = abs(a << 32) // abs(b)
    return q if (a >= 0) == (b > 0) else -q


def fdiv_int(a, n):
    q = abs(a) // abs(n)
    return q if (a >= 0) == (n > 0) else -q


def fparse(text):
    whole, _, frac = text.partition(".")
    m = (int(whole) << 32) + ((int(frac) << 32) // 10 ** len(frac) if frac else 0)
    return m


def fround(a):
    return (a + (ONE >> 1)) >> 32 if a >= 0 else -((-a + (ONE >> 1)) >> 32)


def fd(a):
    return a / ONE


HALF = ONE >> 1


# ---- Hash64 and Pcg32 -------------------------------------------------------------------
def mix(x):
    x = (x + 0x9E3779B97F4A7C15) & MASK64
    x = ((x ^ (x >> 30)) * 0xBF58476D1CE4E5B9) & MASK64
    x = ((x ^ (x >> 27)) * 0x94D049BB133111EB) & MASK64
    return x ^ (x >> 31)


def combine(h, v):
    return mix((h + mix(v & MASK64)) & MASK64)


def of_string(text):
    h = 0xCBF29CE484222325
    for b in text.encode("utf-8"):
        h ^= b
        h = (h * 0x100000001B3) & MASK64
    return h


class Pcg32:
    MULT = 6364136223846793005

    def __init__(self, init_state, seq):
        self.state = 0
        self.inc = ((seq << 1) | 1) & MASK64
        self.next_uint()
        self.state = (self.state + init_state) & MASK64
        self.next_uint()

    @classmethod
    def for_stream(cls, seed, system, entity, tick):
        h = combine(combine(combine(seed & MASK64, system & MASK64), entity & MASK64), tick & MASK64)
        return cls(mix(h), mix(h ^ 0xA5A5A5A5A5A5A5A5))

    def next_uint(self):
        old = self.state
        self.state = (old * self.MULT + self.inc) & MASK64
        xs = (((old >> 18) ^ old) >> 27) & MASK32
        rot = old >> 59
        return ((xs >> rot) | (xs << ((-rot) & 31))) & MASK32

    def below(self, bound):
        threshold = ((1 << 32) - bound) % bound
        while True:
            r = self.next_uint()
            if r >= threshold:
                return r % bound

    def fix01(self):
        return self.next_uint()


# ---- Combat.Fight ------------------------------------------------------------------------
K = fx(6, 100)
DEATH_SHARE = fx(4, 10)


def casualties(enemy, own):
    return fx(1000) if own <= 0 else fdiv(fmul(fmul(K, enemy), enemy), own)


class Side:
    def __init__(self, fighters, cas_mult=None):
        self.f = fighters            # list of (id, strength raw)
        self.down = [False] * len(fighters)
        self.alive = len(fighters)
        self.start = sum(s for _, s in fighters)
        self.strength = self.start
        self.cas_mult = cas_mult     # E3: shields ×0.7, chariots ×0.5 (None = the C# path)

    def lost(self):
        return ONE if self.start == 0 else fdiv(self.start - self.strength, self.start)

    def pick(self, n):
        for i, d in enumerate(self.down):
            if not d:
                if n == 0:
                    return i
                n -= 1
        raise RuntimeError("no such fighter")

    def fall(self, i):
        self.down[i] = True
        self.alive -= 1
        self.strength -= self.f[i][1]
        return self.f[i][0]


def hit(side, expected, rng, dead, wounded):
    if side.cas_mult is not None:
        expected = fmul(expected, side.cas_mult)
    whole = expected >> 32
    frac = expected - (whole << 32)
    count = whole + (1 if rng.fix01() < frac else 0)
    i = 0
    while i < count and side.alive > 0:
        v = side.pick(rng.below(side.alive))
        fid = side.fall(v)
        (dead if rng.fix01() < DEATH_SHARE else wounded).append(fid)
        i += 1


def fight(attackers, defenders, att_mult, def_mult, surprised, att_break, def_break, max_rounds, ranged_first,
          seed, stream, att_cas=None, def_cas=None):
    """Combat.Fight. Returns dict(winner, rounds, a_dead, a_wounded, d_dead, d_wounded, a_surv, d_surv)."""
    a, d = Side(attackers, att_cas), Side(defenders, def_cas)
    rng = Pcg32.for_stream(seed, stream, 0, 0)
    ad, aw, dd, dw = [], [], [], []
    winner = "standoff"
    if ranged_first and a.alive and d.alive:
        shot = casualties(fmul(a.strength, att_mult), fmul(fmul(d.strength, def_mult), HALF))
        hit(d, shot, rng, dd, dw)
    rounds = 1
    while rounds <= max_rounds:
        if a.alive == 0 or d.alive == 0:
            break
        attack = fmul(a.strength, att_mult)
        defend = fmul(d.strength, def_mult)
        if surprised and rounds == 1:
            defend = fmul(defend, HALF)
        on_d = casualties(attack, defend)
        on_a = casualties(defend, attack)
        hit(d, on_d, rng, dd, dw)
        hit(a, on_a, rng, ad, aw)
        d_broke = d.alive == 0 or d.lost() >= def_break
        a_broke = a.alive == 0 or a.lost() >= att_break
        if d_broke and not a_broke:
            winner = "attacker"
            break
        if a_broke:
            winner = "defender"
            break
        rounds += 1
    return dict(winner=winner, rounds=min(rounds, max_rounds), a_dead=ad, a_wounded=aw, d_dead=dd, d_wounded=dw,
                a_surv=a.alive, d_surv=d.alive)


# ---- Raids.Resolve -----------------------------------------------------------------------
BASE_SURPRISE = fx(60, 100)
RAIDER_BREAK = fx(30, 100)
DEFENDER_BREAK = fx(40, 100)
DEFENDER_BREAK_CHILDREN = fx(60, 100)
CARRY = fx(4)
MAX_ROUNDS = 15
SURPRISED_SHARE = fx(40, 100)
ALERT_SHARE = fx(52, 100)
LIFE_VALUE = fx(8)
RELATIONS_COST = fx(6)

# defense multipliers (13 §6.2, 22 §7.2); barrier = one of none/thorn/palisade/hillfort/walls
BARRIER = {"none": ONE, "thorn": fx(13, 10), "palisade": fx(2), "hillfort": fx(5, 2), "walls": fx(3)}
LOOT_MULT = {"hillfort": fx(1, 4), "walls": fx(1, 4)}


def defense_mult(barrier="none", hill=False, archery=False):
    m = fx(11, 10)  # home ground
    if barrier != "none":
        m = fmul(m, BARRIER[barrier])
    if hill:
        m = fmul(m, fx(12, 10))
    if archery and barrier != "none":
        m = fmul(m, fx(5, 4))
    return m


def surprise_chance(alarm=False, dogs=False, watchers=False, night=False, recent=False):
    p = BASE_SURPRISE
    if alarm:
        p = fmul(p, fx(8, 10))
    if dogs:
        p = fmul(p, HALF)
    if watchers:
        p = fmul(p, fx(3, 10))
    if night:
        p += fx(15, 100)
    if recent:
        p -= fx(20, 100)
    return max(0, min(fx(95, 100), p))


def raid(raiders, defenders, d, seed, stream, ranged=False, raid_cas=None, def_cas=None):
    """Raids.Resolve. `d` is a dict: barrier, hill, archery, alarm, dogs, watchers, night, recent."""
    rng = Pcg32.for_stream(seed, stream, 1, 0)
    surprised = rng.fix01() < surprise_chance(d.get("alarm", False), d.get("dogs", False), d.get("watchers", False),
                                              d.get("night", False), d.get("recent", False))
    share = SURPRISED_SHARE if surprised else ALERT_SHARE
    fighting = [f for f in defenders if share >= ONE or rng.fix01() < share]
    b = fight(raiders, fighting, ONE, defense_mult(d.get("barrier", "none"), d.get("hill", False), d.get("archery", False)),
              surprised, RAIDER_BREAK, DEFENDER_BREAK, MAX_ROUNDS, ranged, seed, stream ^ 0x5241494455,
              raid_cas, def_cas)
    success = b["winner"] == "attacker"
    burned = 0
    if success:
        for _ in range(b["rounds"]):
            if rng.below(2) == 0:
                burned += 1
    loot = CARRY * b["a_surv"] if success else 0
    return dict(success=success, surprised=surprised, battle=b, loot=loot, burned=min(burned, 3))


# ---- E3 fighters ---------------------------------------------------------------------------
W = {"bare": fx(6, 10), "club": ONE, "wood": fx(13, 10), "stone": fx(16, 10), "copper": fx(18, 10),
     "arsenical": fx(21, 10), "bronze": fx(22, 10), "tin": fx(23, 10)}
TRAIN = fx(13, 10)
MILITIA = fx(6, 10)
SHIELD = fx(7, 10)
# Crew carry shields: the two reductions stack (0.5 × 0.7 = 0.35; 07d §6, F5).
CHARIOT_CAS = fmul(HALF, SHIELD)


def warriors(n, weapon, first_id=1000, chariot=False):
    s = fmul(fmul(ONE, W[weapon]), TRAIN)
    if chariot:
        s = s * 2
    return [(first_id + i, s) for i in range(n)]


def militia(n, weapon, first_id=0):
    return [(first_id + i, fmul(MILITIA, W[weapon])) for i in range(n)]


def band(adults, weapon, n_warriors=0, warrior_weapon=None):
    """A settlement's fighting-age adults: the first n are warriors (13 §7.1), the rest militia."""
    ww = warrior_weapon or weapon
    out = warriors(n_warriors, ww, first_id=0)
    out += [(n_warriors + i, fmul(MILITIA, W[weapon])) for i in range(adults - n_warriors)]
    return out


# ---- tables ---------------------------------------------------------------------------------
SEED = 20261019


def raid_row(raiders, defenders, d, trials, seed=SEED, raid_shields=False, def_shields=False, loot_mult=ONE):
    succ = rh = dh = rdead = ddead = 0
    net = 0
    rc = fx(7, 10) if raid_shields else None
    dc = fx(7, 10) if def_shields else None
    for t in range(trials):
        o = raid(raiders, defenders, d, seed, t, raid_cas=rc, def_cas=dc)
        b = o["battle"]
        succ += o["success"]
        rdead += len(b["a_dead"])
        ddead += len(b["d_dead"])
        rh += len(b["a_dead"]) + len(b["a_wounded"])
        dh += len(b["d_dead"]) + len(b["d_wounded"])
        loot = fmul(o["loot"], loot_mult)
        # Raids.NetValue(successShare, loot, deaths, life 8, relations 6), summed then ÷ trials as CombatTables.Run
        net += (fmul(ONE, loot) if o["success"] else 0) - fmul(fx(len(b["a_dead"])), LIFE_VALUE) - RELATIONS_COST
    nr, nd = len(raiders), len(defenders)
    return dict(success=100 * succ / trials, raiders_hurt=100 * rh / (trials * nr), def_hurt=100 * dh / (trials * nd),
                raiders_dead=100 * rdead / (trials * nr), def_dead=100 * ddead / (trials * nd),
                net=fd(fdiv_int(net, trials)))


def e2_table(trials=4000):
    """13 §12.1: a warband of 8 (stone 1.6) vs a band of 22 adults (wooden spears) -- the C# CombatTables rows."""
    raiders = [(1000 + i, fx(16, 10)) for i in range(8)]
    dstr = fmul(fx(6, 10), fx(13, 10))
    wstr = fmul(fmul(ONE, fx(13, 10)), fx(13, 10))
    rows = [("none, surprised", {}, 0),
            ("thorn barrier + watchers + dogs", dict(barrier="thorn", watchers=True, dogs=True), 0),
            ("palisade + watchers", dict(barrier="palisade", watchers=True), 0),
            ("palisade + watchers + warriors", dict(barrier="palisade", watchers=True), 8)]
    out = []
    for name, d, nw in rows:
        b = [(i, wstr if i < nw else dstr) for i in range(22)]
        out.append((name, raid_row(raiders, b, d, trials)))
    return out


# §7.5 table 1: a bronze warband of 12 (2.2 × 1.3, shields) against a village of 120 (42 adults)
TABLE1 = [
    # name, defenses, defender weapon, def warriors, def shields, doc 22 (success, raiders hurt, defenders hurt, net)
    ("none, stone-armed militia", {}, "stone", 0, False, (93, 9, 24, 33)),
    ("palisade + watchers, stone militia", dict(barrier="palisade", watchers=True), "stone", 0, False, (13, 39, 8, -16)),
    ("palisade + watchers, bronze militia + shields", dict(barrier="palisade", watchers=True), "bronze", 0, True, (2, 45, 3, -23)),
    ("palisade + watchers + 8 bronze warriors", dict(barrier="palisade", watchers=True), "bronze", 8, True, (1, 60, 3, -29)),
    ("hillfort + watchers + 8 warriors", dict(barrier="hillfort", watchers=True), "bronze", 8, True, (0, 83, 2, -38)),
    ("walls + watchers + 8 warriors", dict(barrier="walls", watchers=True), "bronze", 8, True, (0, 93, 2, -42)),
]
# §7.5 table 2: a bronze warband of 8 against soft targets
TABLE2 = [
    ("mining camp, 6 miners (stone)", {}, 6, "stone", 0, False, (97, 1, 45, 25)),
    ("mining camp, 6 + 2 bronze guards + watch", dict(watchers=True), 8, "stone", 2, True, (99, 4, 42, 24)),
    ("hamlet of 15 adults, stone, no palisade", {}, 15, "stone", 0, False, (100, 1, 37, 25)),
    ("hamlet of 15, bronze, thorn + watch", dict(barrier="thorn", watchers=True), 15, "bronze", 0, True, (89, 11, 26, 17)),
]


def table1(trials, raider_weapon="bronze", shields=True):
    raiders = warriors(12, raider_weapon)
    out = []
    for name, d, wpn, nw, dsh, doc in TABLE1:
        b = band(42, wpn, nw, "bronze")
        lm = LOOT_MULT.get(d.get("barrier", "none"), ONE)
        out.append((name, raid_row(raiders, b, d, trials, raid_shields=shields, def_shields=dsh, loot_mult=lm), doc))
    return out


def table2(trials):
    raiders = warriors(8, "bronze")
    out = []
    for name, d, adults, wpn, nw, dsh, doc in TABLE2:
        b = band(adults, wpn, nw, "bronze")
        out.append((name, raid_row(raiders, b, d, trials, raid_shields=True, def_shields=dsh), doc))
    return out


def field(a, b, trials, a_cas=None, b_cas=None, seed=SEED):
    """Open-ground battle: no defense multiplier, no home ground, both break at 40%; win share of side a."""
    wins = 0
    for t in range(trials):
        r = fight(a, b, ONE, ONE, False, fx(4, 10), fx(4, 10), MAX_ROUNDS, False, seed, t, a_cas, b_cas)
        wins += r["winner"] == "attacker"
    return 100 * wins / trials


def field_battles(trials):
    sh = fx(7, 10)
    res = []
    res.append(("20 bronze+shield vs 20 stone warriors", field(warriors(20, "bronze"), warriors(20, "stone", 5000), trials, sh, None), 100))
    res.append(("20 bronze+shield vs 28 stone warriors", field(warriors(20, "bronze"), warriors(28, "stone", 5000), trials, sh, None), 18))
    for n, doc in ((24, 39), (18, 100), (20, None), (22, None)):
        res.append((f"12 chariot crew vs {n} bronze+shield infantry",
                    field(warriors(12, "bronze", chariot=True), warriors(n, "bronze", 5000), trials, CHARIOT_CAS, sh), doc))
    # stone-equivalent worth of a bronze-and-shield warrior: smallest stone count that wins >= 50%
    worth = None
    for n in range(20, 41):
        if field(warriors(20, "bronze"), warriors(n, "stone", 5000), trials // 2, sh, None) < 50:
            worth = n / 20
            break
    return res, worth


# ---- capture (13 §8.2, F5 at E3 arms) -----------------------------------------------------
def capture_percent(ratio, barrier, militia_weapon, trials, att_weapon="bronze", shields=True, adults=40, seed=SEED):
    """CombatTables.CaptureSuccessPercent with E3 arms: the village's 40 adults are militia with the
    stocked spear; attackers are trained warriors; ratio is of field strength including defenses."""
    village = [(i, fmul(MILITIA, W[militia_weapon])) for i in range(adults)]
    field_str = sum(s for _, s in village)
    dm = defense_mult(barrier)
    a_str = fmul(fmul(ONE, W[att_weapon]), TRAIN) if att_weapon != "e2" else fx(16, 10)
    want = fmul(fmul(field_str, dm), ratio)
    n = fround(fdiv(want, a_str))
    force = [(5000 + i, a_str) for i in range(n)]
    sh = fx(7, 10) if shields else None
    taken = 0
    for t in range(trials):
        r = fight(force, village, ONE, dm, False, fx(4, 10), DEFENDER_BREAK_CHILDREN, MAX_ROUNDS, False, seed, t,
                  sh, sh if militia_weapon in ("bronze", "arsenical", "tin") else None)
        taken += r["winner"] == "attacker" and r["a_surv"] >= 4
    return 100 * taken / trials, n


# ---- the levy host and investment (22 §4.3, §7.4) ------------------------------------------
def levy_host(people=220, adult_share=0.36, warrior_share=0.10, in_reach=0.8):
    """Raise the levy: all warriors plus 50% of the other adults of settlements within 2 seasons' travel.
    Returns (figures, strength): warriors 2.2 × 1.3, levied adults base 1.0 × bronze 2.2 (13 §7.1)."""
    adults = people * adult_share * in_reach
    w = people * adult_share * warrior_share
    levied = 0.5 * max(0.0, adults - w)
    return w + levied, w * 2.86 + levied * 2.2


def invest(stores0, besieger_str, defender_str, trials=4000, supplied=True, inside_income=0.0, seed=SEED,
           cap_low=0.35, cap_zero=0.70, defenders=35, people=100, max_seasons=16):
    """§7.4 investment of a walled town, unrelieved. Stores in seasons of need. Each season the town eats one
    season of need less `inside_income` (fields and pastures yield nothing). At season end: stores >= 1 season, no
    capitulation; below, `cap_low`; at zero, `cap_zero` plus the winter-shortfall starvation hazard on the unfed share
    (0.25 children and elders, 0.10 adults). Unsupplied besiegers carry one season of food, then lose 10% a season to
    desertion; the siege lifts when they fall below 1.2× the unwalled defenders or the defenders can sally
    (1.1 × defenders > 0.8 × besiegers: the sally is fought in the open). Returns the distribution of fall season."""
    sys_id = of_string("politics.siege")
    fall = [0] * (max_seasons + 2)
    for t in range(trials):
        stores, b, dstr, food = stores0, besieger_str, defender_str, 1 if not supplied else 10 ** 9
        fell = max_seasons + 1
        for season in range(1, max_seasons + 1):
            stores = max(0.0, stores - (1.0 - inside_income))
            if not supplied:
                food -= 1
                if food < 0:
                    b *= 0.9
            rng = Pcg32.for_stream(seed, sys_id, t, season)
            if stores <= 1e-9:
                dstr *= 1 - 0.10          # unfed adults (FS 0): 10% of the fighters die or are too weak
            if b < 1.2 * dstr or 1.1 * dstr > 0.8 * b:
                break                     # lifted or broken by a sally (not a capitulation)
            p = 0.0 if stores >= 1.0 - 1e-9 else (cap_low if stores > 1e-9 else cap_zero)
            if p > 0 and rng.fix01() < fx(int(round(p * 1000)), 1000):
                fell = season
                break
        fall[fell] += 1
    return fall


def siege_summary(fall, lo=3, hi=6):
    n = sum(fall)
    inwin = sum(fall[lo:hi + 1]) / n
    by = lambda s: sum(fall[1:s + 1]) / n  # noqa: E731
    cum = 0
    med = None
    for s, c in enumerate(fall):
        cum += c
        if med is None and cum >= n / 2:
            med = s
    return inwin, by(2), by(6), med


# ---- report -----------------------------------------------------------------------------------
def pct(x):
    return f"{x:5.1f}"


def main(trials=4000):
    flags = []
    print(f"== E2 reference (13 §12.1), {trials} raids a row: the port must equal `politics --report-combat`")
    for name, r in e2_table(trials):
        print(f"  {name:36s} success {pct(r['success'])}  raiders hurt {pct(r['raiders_hurt'])}  defenders hurt "
              f"{pct(r['def_hurt'])}  dead {r['raiders_dead']:.1f}/{r['def_dead']:.1f}  net {r['net']:6.1f}")

    print(f"\n== T21 §7.5 table 1: bronze warband of 12 (2.2 × 1.3, shields) vs a village of 120 (42 adults); doc 22 in brackets")
    for name, r, doc in table1(trials):
        d_s = r["success"] - doc[0]
        mark = "" if all(abs(actual - expected) <= 10 for actual, expected in zip(
            (r["success"], r["raiders_hurt"], r["def_hurt"], r["net"]), doc)) else "  << moves"
        if mark:
            flags.append(("T21 t1", name, r, doc))
        print(f"  {name:46s} success {pct(r['success'])} [{doc[0]:3d}]  raiders hurt {pct(r['raiders_hurt'])} [{doc[1]:3d}]"
              f"  defenders hurt {pct(r['def_hurt'])} [{doc[2]:3d}]  net {r['net']:6.1f} [{doc[3]:4d}]{mark}")
    stone = table1(trials, raider_weapon="stone", shields=False)[0][1]
    stone_doc = (63, 18, 17, 15)
    if any(abs(actual - expected) > 10 for actual, expected in zip(
            (stone["success"], stone["raiders_hurt"], stone["def_hurt"], stone["net"]), stone_doc)):
        flags.append(("T21 stone", "the 12 with stone spears, no shields, vs none", stone, stone_doc))
    print(f"  {'(the 12 with stone spears, no shields, vs none)':46s} success {pct(stone['success'])} [{stone_doc[0]:3d}]  raiders hurt "
          f"{pct(stone['raiders_hurt'])} [{stone_doc[1]:3d}]  defenders hurt {pct(stone['def_hurt'])} [{stone_doc[2]:3d}]  net {stone['net']:6.1f} [{stone_doc[3]:4d}]")

    print(f"\n== T21 §7.5 table 2: bronze warband of 8 vs soft targets")
    for name, r, doc in table2(trials):
        mark = "" if all(abs(actual - expected) <= 10 for actual, expected in zip(
            (r["success"], r["raiders_hurt"], r["def_hurt"], r["net"]), doc)) else "  << moves"
        if mark:
            flags.append(("T21 t2", name, r, doc))
        print(f"  {name:46s} success {pct(r['success'])} [{doc[0]:3d}]  raiders hurt {pct(r['raiders_hurt'])} [{doc[1]:3d}]"
              f"  defenders hurt {pct(r['def_hurt'])} [{doc[2]:3d}]  net {r['net']:6.1f} [{doc[3]:4d}]{mark}")

    print("\n== Field battles (open ground, break 40% both)")
    res, worth = field_battles(trials // 2)
    for name, v, doc in res:
        if doc is not None and abs(v - doc) > 10:
            flags.append(("T21 field", name, {"success": v, "raiders_hurt": 0, "def_hurt": 0, "net": 0}, (doc, 0, 0, 0)))
        print(f"  {name:46s} win {pct(v)}" + (f" [{doc}]" if doc is not None else ""))
    print(f"  a bronze-and-shield warrior is worth {worth:.2f} stone-armed warriors (doc 22 §7.1: 1.35-1.4)")

    print("\n== T22 capture within one season, a village of 40 adults (F5 at E3 arms)")
    for barrier in ("palisade", "hillfort"):
        for mw in ("stone", "bronze"):
            for ratio in ("1.0", "1.5", "2.0"):
                p, n = capture_percent(fparse(ratio), barrier, mw, trials // 4)
                print(f"  {barrier:9s} {mw:6s} militia, attackers ×{ratio}: {pct(p)}% held ({n} bronze warriors)")
    p, n = capture_percent(fparse("1.5"), "palisade", "bronze", 400)
    print(f"  doc 22 §7.3 check: a palisaded village of 40 bronze militia needs {n} bronze warriors at 1.5× (doc: about 61)")

    print("\n== Levy host (22 §4.3): civ of 220 people, 36% adults, 10% of adults warriors, 80% of settlements in reach")
    figs, s = levy_host()
    vil = 40 * 0.6 * 2.2 * 1.1
    print(f"  host {figs:.0f} figures, strength {s:.0f}; a walled village of 40 bronze militia: unwalled {vil:.0f}, "
          f"investment needs {1.2 * vil:.0f} -> {'allowed' if s >= 1.2 * vil else 'refused'}; walled field strength {vil * 3:.0f}")

    print("\n== T22 investment (§7.4): unrelieved walled town, supplied besiegers at 2× the unwalled defenders")
    print("   stores  inside  capitulation   in 3-6 seasons   by season 2   by 6   median")
    for inside in (0.0, 0.25):
        for st in (2.0, 2.5, 3.0):
            for lo, zero in ((0.35, 0.70),):
                f = invest(st, 2.0, 1.0, trials // 2, inside_income=inside, cap_low=lo, cap_zero=zero)
                w, b2, b6, med = siege_summary(f)
                print(f"   {st:4.1f}   {inside:4.2f}    {lo:.2f}/{zero:.2f}      {100 * w:5.1f}%          {100 * b2:5.1f}%     "
                      f"{100 * b6:5.1f}%   {med}")
    f = invest(2.5, 2.0, 1.0, trials // 2, supplied=False)
    w, b2, b6, med = siege_summary(f)
    print(f"   unsupplied besiegers, stores 2.5: in 3-6 {100 * w:.1f}%, by 6 {100 * b6:.1f}% (desertion lifts many sieges)")
    for st in (2.0, 2.5, 3.0):
        f = invest(st, 2.0, 1.0, trials // 2, cap_low=0.25, cap_zero=0.50)
        w, b2, b6, med = siege_summary(f)
        print(f"   alternative 0.25/0.50, stores {st}: in 3-6 {100 * w:.1f}%, by 2 {100 * b2:.1f}%, median {med}")

    if flags:
        print("\nCells outside ±10 of the corrected doc 22 §7.5 values (the port is the oracle):")
        for tab, name, r, doc in flags:
            print(f"  {tab}: {name}: success {r['success']:.0f} vs {doc[0]}, raiders hurt {r['raiders_hurt']:.0f} vs {doc[1]},"
                  f" defenders hurt {r['def_hurt']:.0f} vs {doc[2]}, net {r['net']:.1f} vs {doc[3]}")
    return flags




def selftest():
    # Fixed-point and RNG reference values
    rng = Pcg32(42, 54)
    first = [rng.next_uint() for _ in range(3)]
    assert first == [0xA15C02B7, 0x7B47F409, 0xBA1D3330], [hex(x) for x in first]   # pcg32 published stream
    assert fx(6, 100) == 257698037 and fmul(fx(3, 2), fx(3, 2)) == fx(9, 4)
    rows = e2_table(4000)
    got = [(round(r["success"], 1), round(r["raiders_hurt"], 1), round(r["def_hurt"], 1)) for _, r in rows]
    print("E2 port:", got)
    # 13 §12.1 published (C# CombatTables, seed 20261019): 83 / 37 / 12 / 4.5 success
    for (s, _, _), pub in zip(got, (83, 37, 12, 4.5)):
        assert abs(s - pub) < 1.0, (s, pub)
    t1 = table1(1000)
    assert all(all(abs(actual - expected) <= 10 for actual, expected in zip(
        (r["success"], r["raiders_hurt"], r["def_hurt"], r["net"]), doc)) for _, r, doc in t1), "T21 table 1 outside ±10"
    assert t1[0][1]["success"] > 80 and t1[2][1]["success"] < 15, "bronze makes unwalled villages prey, palisade+bronze safe"
    assert all(r["success"] < 2.5 for _, r, _ in t1[3:]), "8 bronze warriors behind a barrier stop a raid of 12"
    t2 = table2(1000)
    assert all(all(abs(actual - expected) <= 10 for actual, expected in zip(
        (r["success"], r["raiders_hurt"], r["def_hurt"], r["net"]), doc)) for _, r, doc in t2), "T21 table 2 outside ±10"
    assert t2[0][1]["success"] > 85, "mining camps are prey"
    stone = table1(1000, raider_weapon="stone", shields=False)[0][1]
    assert all(abs(actual - expected) <= 10 for actual, expected in zip(
        (stone["success"], stone["raiders_hurt"], stone["def_hurt"], stone["net"]), (63, 18, 17, 15))), "T21 stone-warband row outside ±10"
    battle_rows, _ = field_battles(1000)
    assert all(doc is None or abs(actual - doc) <= 10 for _, actual, doc in battle_rows), "T21 field-battle row outside ±10"
    p, _ = capture_percent(fparse("1.5"), "palisade", "bronze", 300)
    assert p >= 60, p
    w, *_ = siege_summary(invest(2.5, 2.0, 1.0, 1000))
    assert w >= 0.5, w
    print("combat_e3 selftest passed")


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--selftest" in args:
        selftest()
    else:
        n = int(args[args.index("--trials") + 1]) if "--trials" in args else 4000
        if main(n):
            raise SystemExit("combat_e3 oracle reported a T21 row outside tolerance")
