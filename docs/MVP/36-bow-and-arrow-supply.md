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
| G-BOW, bow and arrows | HNT-06 (hunting), WAR-03 (levy stock) | 0.05 wood, 0.1 flint | 0.4 | any settlement (E0–E2 craft step) | 24 seasons in store; 10 work-seasons in a hunter's hands | 4 |
| G-LONGBOW, self-bow of yew or ash with arrows | WAR-11 | 1 wood, 0.1 cord | 0.4 | any settlement (craft) | 24 seasons | 4 |
| G-CROSSBOW, with bolts | WAR-11 | 1 wood, 0.1 wrought iron (or bloom), 0.2 cord | 0.6 | a settlement with a **forge** (the iron nut, trigger and stirrup are a smith's work) | 40 seasons | 6 |

- The **G-BOW** is the same good hunters already use: the hunter's bow is the war bow. Before Archery Warfare a town makes one
  hunting bow at the old E2 price (10 work). In **every chapter**, once WAR-03 is known, bow-making is a routine craft (0.4 work,
  only 0.05 L of wood for a stave and a few shafts) and the town keeps a levy stock. A town makes **at most three bows a season**,
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
- **Surplus only:** no new bows in a season when food security is under 0.8 (the line that already halves the metal chain);
  never more than half the cut wood above the 12 L hearth-and-building reserve in a season; bows are the last E0–E2 craft
  claim after tools (G-BOW takes at most three bows' work, 1.2 of the leftover craft work), and the WAR-11 want is a *secondary* metal-plan
  want, after kits and iron arms.
- **What that gives in the fixture** (a 100-person town, wood topped up once): three G-BOWs in the first season after WAR-03, and 5+
  after four seasons once wear is counted; with WAR-11 about 22 longbows and 3 crossbows in 4 seasons while the wood lasts, 7 longbows without any
  iron. In practice wood and craft work, not the cap, set the stock.

## 5. Wear and loss

- **Age wear** (each season): crossbows (Fief only) lose 1/40 of the stock, longbows 1/24, stored G-BOWs in every chapter beyond the one in use
  1/24 (strings rot, staves lose their cast, arrows are spent). The bow in a hunter's hands still wears by hunting work as before.
- **Levies who die** lose their weapon. Combat calls `LivingEconomyModel.LoseArms(settlement, goodId, n)`, which removes up to
  `n` and returns what it removed (works for G-BOW, G-CROSSBOW, G-LONGBOW and the metal arms). Recommended rule: one weapon
  lost per dead archer, of the kind that archer carried. The winner takes nothing from the field (keeps loot simple).
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

## 9. Open questions

1. **Pace.** Settled at three bows a season (the owner's call); the cap of 40 per town now sets how long a full levy takes (about a dozen seasons).
2. ~~All chapters~~ Done: the early bow is real in Bronze and Iron too; WAR-11 stays Fief-only.
3. **Longbow training.** History says the longbow needs years of practice; the oracle says a levy needs none. Kept the oracle.
4. **Ammunition** as its own good, if sieges ever need it (§2).
