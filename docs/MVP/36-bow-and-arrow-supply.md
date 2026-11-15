# 36. Bows and arrows as real arms (WAR-03 bows, WAR-11 crossbows and longbows)

Status: built in the sim (economy side), 2026-11-15, branch `codex/war11-bow-goods`. Combat reads the stock (see §7); that hook
lives in `src/CivSim.Sim/Politics/` and is the combat branch's work. All numbers are provisional and sit inside the
`e5_combat_oracle.py` reading (a bolt or arrow weapon is strength 2.0 in a levy's hands, no drilling needed).

## 1. The short version (what to accept)

- **Bows become things a town makes and keeps,** not a flat bonus for knowing the technology. A fighter counts as an archer only
  if a bow is in store for them.
- **Three goods:** the old **Bow and arrows** (G-BOW, from Archery Warfare WAR-03), and from WAR-11 the **Crossbow** (G-CROSSBOW)
  and the new **Longbow and arrows** (G-LONGBOW).
- **Arrows and bolts are part of the weapon,** not a separate good. The bow's wear stands for arrows spent and lost.
- **A town wants one bow per fighting-age person** (14 or older, not a paid soldier), **at most 40 per settlement**, and builds
  them only from spare work, spare wood and in a season with enough food. Tools and food come first.
- **Bows wear out** (a stored bow lasts about 6 years; a crossbow 10), **are lost when the levy falls**, and **burn with a razed
  town**. A captured town's bows change hands with it.
- **The early bow is real in every chapter; crossbows and longbows (WAR-11) only in Faith & Fief matches.** The politics and research goldens
  were refreshed for the bow change (§8).

## 2. Arrows and bolts: folded into the weapon (recommended)

Option A, separate arrow and bolt goods spent per battle, needs a second stock, a refill want, a per-battle spend rule and a
"bow with no arrows" state in combat. The oracle has no ammunition term at all: strength 2.0 assumes the archer can shoot. Option
B folds a sheaf of arrows (or a case of bolts) into each weapon and lets the weapon's wear stand for the arrows used up. B stays
inside the oracle's numbers, needs no new combat rule, and matches how the registry already names G-BOW ("Bow and arrows").
**Recommendation: B.** If ammunition ever matters (sieges, long campaigns), it can be added later as a separate good without
changing these three.

## 3. The goods and how they are made

| Good | Unlocked by | Recipe (per weapon) | Work | Where | Lasts | Barter |
|---|---|---|---|---|---|---|
| G-BOW, bow and arrows | HNT-06 (hunting), WAR-03 (levy stock) | 0.05 wood (no flint) | 0.4 | any settlement (E0–E2 craft step) | 24 seasons in store; 10 work-seasons in a hunter's hands | 4 |
| G-LONGBOW, self-bow of yew or ash with arrows | WAR-11 | 1 wood, 0.1 cord | 0.4 | any settlement (craft) | 24 seasons | 4 |
| G-CROSSBOW, with bolts | WAR-11 | 1 wood, 0.1 wrought iron (or bloom), 0.2 cord | 0.6 | a settlement with a **forge** (the iron nut, trigger and stirrup are a smith's work) | 40 seasons | 6 |

- The **G-BOW** is the same good hunters already use: the hunter's bow is the war bow. Before Archery Warfare a town makes one
  hunting bow at the old E2 price (10 work). In **every chapter**, once WAR-03 is known, bow-making is a routine craft (0.4 work,
  only 0.05 L of wood for a stave and a few shafts, bone-headed and fire-hardened arrows so no flint) and the town keeps a levy stock. A town makes **at most three bows a season**,
  the first one included (three times the first draft's pace of about one); the cap, not a share of craft work, is the throttle.
- **Crossbow or longbow?** Crossbows are wanted first, as many as the forge and the iron **already in store** can make (kits
  are forged first, so crossbows never take iron from tools). The rest of the season's want is longbows, which need no iron.
  The longbow is the iron-poor town's levy weapon.
- After WAR-11 the town stops replacing its old G-BOWs for the levy (it keeps its hunting bow); crossbows and longbows replace
  them as they wear out.
- Both WAR-11 recipes are crafts (order 66): they run on the craft work left after the E0–E2 crafts and after the metal chain's
  kits and arms.

## 4. How many a town holds

- **Target:** one weapon per resident aged 14 or more who is not a professional soldier (soldiers carry iron arms), capped at
  **40 per settlement**. G-BOW counts toward the target before WAR-11; crossbows plus longbows after it.
- **Surplus only:** no new bows in a season when food security is under 0.8 (the line that already halves the metal chain); crossbows
  and longbows (WAR-11) never take more than half the cut wood above the 12 L hearth-and-building reserve in a season, while the war bow's
  0.05 L needs no reserve rule (the first version had one, and real towns almost never held that much wood: see §8); bows are the last E0–E2 craft
  claim after tools (G-BOW takes at most three bows' work, 1.2 of the leftover craft work), and the WAR-11 want is a *secondary* metal-plan
  want, after kits and iron arms.
- **What that gives in the fixture** (a 100-person town, wood topped up once): three G-BOWs in the first season after WAR-03, and 5+
  after four seasons once wear is counted; with WAR-11 about 22 longbows and 3 crossbows in 4 seasons while the wood lasts, 7 longbows without any
  iron. In practice wood and craft work, not the cap, set the stock.

## 5. Wear and loss

- **Age wear** (each season): crossbows (Fief only) lose 1/40 of the stock, longbows 1/24, stored G-BOWs in every chapter beyond the one in use
  1/24 (strings rot, staves lose their cast, arrows are spent). The bow in a hunter's hands still wears by hunting work as before.
- **Levies who die** lose their weapon (built, 2026-11-16). Every battle's casualties go through `PoliticsSystem.ApplyCasualties`, which now
  charges each death to the dead fighter's settlement: the town's bow stock as a share of the fighters it could field, crossbows first, then
  longbows, then the war bow (the order `Fighters()` draws them in), taken as a fraction of a bow through `LoseArms`, so never more than one bow
  per death. The fight does not record which fighter held which bow, so the loss is the expected one, not an exact one. A war bow counts only from
  Archery Warfare (before it the single bow in store is the hunter's tool); crossbows and longbows only in a Fief match. Wounded fighters keep their bows.
- **A razed settlement** loses all its bows; **a captured settlement** keeps them under its new owner.
- **Fission:** G-BOWs split with the other tools as today; crossbows and longbows stay with the parent town.

## 6. Trade and barter

Bows are arms: they are **not offered in barter** (they are not in `TradeGoodsValue`, the same as iron arms) and **are not shared**
between sister settlements (every town can make longbows itself). `TransferGood` can move them if a later loot or tribute rule
wants it. Barter values (4, 4, 6) are set for that day.

## 7. Combat contract and the abstract 1.8 bow

Combat reads `GoodStock(settlement, "G-BOW" | "G-CROSSBOW" | "G-LONGBOW")`, floors it, and gives one weapon to one levy fighter,
best first: crossbow, then longbow, then bow. Strength (oracle): crossbow and longbow 2.0 for an untrained levy, bolts ignore the
shield casualty cut and arrows do not; a G-BOW keeps today's 1.8 (x1.5 with WAR-03).

`PoliticsSystem.Weapon()` used to give **every** fighter of a civilization with HNT-06 a ranged 1.8 (x1.5 with WAR-03) whether or
not a bow existed. To avoid counting bows twice the ranged branch now applies only to fighters who draw a bow from stock; everyone else falls back to the best weapon the civilization knows (HNT-05 1.7, HNT-03 1.6, HNT-02
1.3, WAR-01 1.0) or their metal arms. In **every chapter** `Fighters()` uses a new `FieldWeapon()` (the same ladder without the bow rung) for the fighters who hold no bow, so the bow's 1.8 (x1.5 with WAR-03) is earned by holding one. `Weapon()`/`WeaponOf()` keep the bow rung: that is the weapon a civilization is *believed* to carry (the fog-of-war belief ladder), not the strength a fighter gets. Crossbows and longbows are drawn from stock only in a Fief match.

## 8. Goldens and determinism

- The bow recipes and the levy stock run in **every chapter**; only WAR-11's crossbows and longbows are gated on the Fief chapter (their stock
  lives outside the Bronze and Iron hashes and snapshots, hashed only while a town holds one). G-BOW sits in the ordinary stores, so no snapshot
  changes for it. The politics and research goldens move because early-bow strength now depends on stock and on the craft work and wood bows use
  (see the commit message for the new digests). The toy golden does not. The content-pack golden changes because the content changed
  (G-LONGBOW, make_longbow, the forge on make_crossbow, the WAR-11 good effect).
- The Fief snapshot version goes from 34 to 35 (a new levy-bow section).

### Balance check (2026-11-16)

`iron-match` over six seeds (20261019 to 20261024), Iron and `--bronze-control`, against the build where bows were Fief-only:

| | raids | assaults | captured | wars | legacy | peak soldiers |
|---|---|---|---|---|---|---|
| Iron, Fief-only bows (baseline) | 16 | 4 | 3.3 | 2.7 | 20379 | 595 |
| Iron, first version (wood above a reserve, and flint) | 12.7 | 2 | 2 | 2.7 | 20228 | 600 |
| Iron, final (0.05 L of wood, no flint) | 15.7 | 2.3 | 2.3 | 1.7 | 20192 | 622 |
| Bronze, Fief-only bows (baseline) | 15.3 | 3 | 2.3 | 2.3 | 14266 | - |
| Bronze, first version | 12 | 1 | 1 | 1.7 | 14208 | - |
| Bronze, final | 14.7 | 5 | 5 | 2.7 | 14303 | - |

The first version made raids and assaults fall because real towns almost never built a bow: at season 300 none of 93 towns held one, since only 13 held
more wood than the reserve and fewer than half held flint (the unit tests had stocked both by hand). The final rule gives about nine bows a town by season
180. Raids, legacy and soldiers are back at the baseline; assaults, captures and wars swing between 0 and 12 from seed to seed and six seeds cannot
settle them, so a larger sample is worth running before any pace tuning.

Second check, with bow loss on death (six seeds, same as above): Iron raids 15.3, assaults 1.3, captures 1, wars 1.3, legacy 20435, peak soldiers 603;
Bronze raids 17, assaults 2.3, captures 2.3, wars 2.3, legacy 14085. Raids, legacy and soldiers stay at the baseline. Iron assaults and captures are
lower than the baseline's 4 and 3.3 in both bow builds (and lowest with bow loss), which may be real (a bow stock is defence strength too) or seed noise;
it is the first thing a larger sample should settle.

Third check, larger sample (2026-11-15): `iron-match`, 24 paired seeds (20261101 to 20261124), both runs on the same commit with the bows on
(`LevyBowsPerSeason = 3`) against off (`LevyBowsPerSeason = 0`, so no bow is ever made, held or lost). The earlier six-seed baseline was a different
commit with other sessions' Iron and Politics changes mixed in, so this one isolates the bows. Means over the 24 seeds, difference (on minus off) with its
standard error: wars 1.83 to 2.33 (+0.50, se 0.68), raids 13.8 to 16.2 (+2.3, se 1.4), assaults 1.92 to 2.25 (+0.33, se 0.72), captures 1.92 to 2.17
(+0.25, se 0.65), chapter end 409.8 to 411.0 seasons (+1.2, se 1.7), legacy 5036 to 5045 (+9, se 31), peak soldiers 648 to 674 (+27, se 16), settlements 102.3
to 104.5 (+2.1, se 1.1), people 8240 to 8506 (+266, se 126). All 48 matches end by the rival countdown. Nothing moves against the bows: the six-seed dip in
assaults and captures was noise. The only differences near two standard errors (people and settlements, a few percent) point up, not down. No pace tuning is
needed.

## 9. Open questions

1. **Pace.** Settled at three bows a season (the owner's call); the cap of 40 per town now sets how long a full levy takes (about a dozen seasons).
2. ~~All chapters~~ Done: the early bow is real in Bronze and Iron too; WAR-11 stays Fief-only.
3. **Longbow training.** History says the longbow needs years of practice; the oracle says a levy needs none. Kept the oracle.
4. **Ammunition** as its own good, if sieges ever need it (§2).
