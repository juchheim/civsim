# 35 — Crossbow and longbow levy (WAR-11): the on-map art

Status (2026-11-15, later): **steps 1 to 4 are built**: the two figures with their tailored `_g` variants (`art_fief_people.gd`), the gallery row
(`--scenario=fief-gallery --fief-only=levy`), the look bit (`IronFrame.LookCrossbows` = 32, WAR-11, `IRON_CROSSBOWS` with `--looks-debug` filling 63),
the one weapon rule `_arms_mesh` with `_bow_mesh` (`world_builder.gd`) used by walking figures, the castle garrison and the drill ground (slots 4 and 5 are
bows, or gunners with gunpowder; bows and gunners on the castle ring face outward). A longbowman was photographed in the live map and the gallery shows both
figures at the soldier's height. **Not built:** step 5 (the skirmish volley) and step 6 (bowmen on a besieged wall). The garrison and drill-ground
slots are wired and run without errors but were not photographed. Original status: design, 2026-11-15. It expands the one row of [32 §6](32-e5-art-pass.md) ("Crossbow or longbow levy |
peasant cloth, a crossbow or a long bow, a quiver | in defense, on walls, in battle | P2") into a buildable spec. Numbers marked (P) are
proposals to tune in the gallery.

Sources read: [32](32-e5-art-pass.md) (§2 kit, §6 figures, §9 data, §10 status), [19](19-art-direction.md), `content/catalog/war-arms-warfare.yaml`
on this branch and on `codex/e5-fief` (worktree `civsim-e5`), `docs/MVP/balance/tools/e5_combat_oracle.py` (on `codex/e5-fief`),
`art_fief_people.gd`, `mesh_kit.gd` (`_person`), `skirmish_rig.gd`, `world_builder.gd` (`_figures`, `_drill`, `_castle`, `_stone_walls`),
`core_layout.gd` (castle garrison, drill ground), `world_data.gd`, `ViewTypes.cs`, `FrameBuilder.cs`, `FrameBuilder.Iron.cs`, `IronFrame.cs`,
`LowPoly3DView.gd` (`show_fief_gallery`), `Game.cs` (how looks reach Godot).

## 0. In plain words

When a civilization learns **Crossbows & Longbows**, its ordinary people who are called up to fight (the levy) should be seen carrying a
crossbow or a longbow instead of a spear, and a couple of the soldiers on its castle walls and drill ground should be bowmen. Nothing else
changes on the map.

The recommendations, each of which you can accept as written:

1. **Two new figures**, `person_crossbow` (a quilted jacket, an iron kettle hat, a crossbow levelled at the chest, a bolt case at the hip and
   a big shield on the back in the owner's colour) and `person_longbow` (a hood, a tall bow drawn to the cheek, a quiver over the shoulder).
   Each is about 400 triangles, the same size as the soldier it stands beside.
2. **One bow style per civilization**, fixed for the whole match, so a civilization never shows both. Half the civilizations draw
   crossbows and half longbows (Q1).
3. **One rule chooses every armed figure's weapon**, used everywhere (walking people, garrison, drill ground). The person's own stable
   random number decides, so a person keeps the same weapon from frame to frame. Ordinary people called up (not the trained warriors) get
   the bow; about three in ten of the trained warriors do too. Once the civilization has gunpowder, that top three in ten become gunners
   instead, so bows thin out but do not vanish.
4. **The data goes in the Iron block's look bits** (`IronFrame.CivLooks`, a whole int per civilization with 27 bits free), not in
   `MatchFrame.CivLooks`, which is now full. Faith & Fief matches already fill that block, so no new frame field is needed.
5. **Build in six small steps**, meshes and a gallery shot first, then the look bit, then the walking figures, then the garrison and drill
   ground, then the skirmish volley, and last the defenders on a besieged wall.

## 1. What the sim has, and what it does not

Doc 32's first rule is "draw only what the sim has". So first, what the sim actually holds for WAR-11.

| Where | What WAR-11 is there |
|---|---|
| This branch (`codex/e5-art-p0`) | A prose-only draft: "Ranged troops pierce armor; peasant levies effective." No good, no recipe, no rule. |
| `codex/e5-fief` content | Typed: grants the good **G-CROSSBOW** (`goods.yaml`, arms, 40 seasons, load 0.5) and the recipe **make_crossbow** (wood 1, iron or wrought 0.1, cord 0.2). The combat text: a levy ranged weapon of **strength 2.0 for an undrilled levy**, a **first-round volley**, and **bolts ignore the shield casualty cut**. |
| `codex/e5-fief` sim code | **Nothing yet.** No C# file mentions crossbows: the good loads from the pack but is not a `FiefGood` slot, no one holds it as a kit, and the combat rule is not in `Combat`. The numbers live only in `e5_combat_oracle.py` (30 crossbow levies beat 20 steel infantry; "pierce armor" is worth about a tenth). |
| Either branch | **No longbow at all.** The project is named "Crossbows & Longbows", but there is one good and one combat row, a "levy ranged weapon". |
| Either branch | WAR-11 is era 5, so it can only be **completed in a Faith & Fief match**. In Bronze and Iron no civilization ever has it. |

What the sim does have that the art can lean on:

- **Who is armed.** Each figure in the frame has the `Armed` flag (256): the village's warriors, every member of a raid or assault party,
  and people holding an occupied town (`FrameBuilder.cs` around line 775 to 811).
- **Who is a trained warrior.** The `Warrior` flag (128) marks about one adult man in five once WAR-04 is known. A party member who is armed
  but **not** a warrior is an ordinary adult called up, which is exactly the "peasant levy" WAR-11 is about. (Honest caveat: the warrior
  flag is itself a view-side estimate, one in five adults, not a sim role list. It is stable and deterministic, which is all the art needs.)
- **The garrison count.** `IronFrame.Soldiers` per own settlement drives the castle guards and the drill ground (up to six each).
- **Sieges.** `IronFrame` sieges give the besieged town, the besieger, the integrity and the direction the attack comes from.
- **Project knowledge per civilization.** Looks are cosmetic bits, "what this civilization knows" (32 §9), exactly like the knights and
  gunners just added for WAR-10 and WAR-12.

So the honest rule for now is the same one the knights and gunners use: **the figure changes when the civilization knows WAR-11.** When
the sim later decides who actually holds a crossbow (Q3), the art should switch to that per-figure fact without any new mesh.

## 2. The two figures

### 2.1 Shared frame (match the soldier, not the other E5 figures)

Both figures are built in the frame of `_person` in `mesh_kit.gd`, the one `person_soldier` uses: legs from 0 to 0.036, the torso block
lifted by 0.034, the head centred at about 0.084, total height about **0.097** (the "adult about 0.1 tall" of 32 §2), facing +Z, right hand
at (0.026, 0.042, 0.012) after the lift.

> **Finding to check in the gallery.** The E5 figures already in `art_fief_people.gd` do not apply that 0.034 lift: by the code the gunner's
> cap tops out near 0.087, the pilgrim's hat near 0.072 and the monk's head near 0.063, against about 0.097 for `person_soldier`. In a
> match a monk would stand about two thirds the height of the villager beside him. The file header ("an adult is ~0.06 tall") repeats the
> unlifted numbers. The levy figures should use the lifted frame; the existing ones deserve a side-by-side gallery check (Q6).

Common parts (from `_person`, same colours and tags so the levy belongs to the crowd it was called from):

- legs `#4a3a2a`, two 5-sided prisms;
- tunic body tagged `TAG_CLOTH` (toned per instance like every villager);
- **the owner's colour mantle** over the shoulders (`TAG_CIV`), the main identity read from above, plus the sash band;
- head and hair as `_person`;
- the `_g` (tailored clothing, FIB-04) variant with parka, fur hem and boots, because nearly every civilization reaching E5 has FIB-04
  and a bare-legged levy would stand out from its own village. The gunner currently has no `_g` variant (Q5).

### 2.2 `person_crossbow`: the crossbowman, levelled

**Silhouette.** A pale, padded body with a flat-brimmed iron hat and a short **horizontal bar** (the bow) held level across the chest.
From above, a coloured rectangle on the back (the pavise). Read at overview zoom: "a T in front of a pale figure". Nothing else in the kit
has a horizontal weapon.

**Pose.** Aiming straight ahead: both forearms forward, the stock against the right shoulder, the prod (bow) at the front, at chest height
(y about 0.06). One static pose; no animation is needed on the map.

**Palette.** Quilted jack (gambeson) in undyed linen `#b8a888` with two darker quilting bands `#9c8c6c` (distinct colour mass from the
soldier's iron grey and the gunner's powder brown); stock `WOOD` `#5e4631`; prod in wrought iron, the kit's tier-4 colour `#3f444c`; string
`#2f2418`; kettle hat in iron `#5f666e` with a lighter brim `#8d949c`; pavise face `TAG_CIV`, rim `WOOD`.

**Owner's colour.** The mantle (as every person) and the **pavise** on the back, a large flat civ-coloured face that reads from any camera
angle. Historically the crossbowman's shield; here it is also the cue that tells crossbow from longbow at a distance.

| Part | Builder calls | Triangles (approx.) |
|---|---|---|
| Legs, tunic (as quilted jack), two quilt bands, mantle, sash | 2 prism(5), 3 prism(7), 2 prism(7) | 30 + 63 + 42 = 135 |
| Arms forward, bent at the elbow | 4 pole | 48 |
| Head and hair | blob(7,4), blob(7,3) | 70 |
| Kettle hat: crown and brim | blob(7,3), prism(8) flat | 28 + 24 = 52 |
| Crossbow: stock, prod (two angled limbs), string (two), stirrup loop | box, 2 pole, 2 pole, 2 pole | 12 + 72 = 84 |
| Bolt case at the right hip with three bolt ends | box, 3 small box | 12 + 36 = 48 |
| Pavise on the back: board, owner's-colour face, boss | box, quad, small box | 12 + 2 + 12 = 26 |
| **Total** | | **about 460; budget 480** |

Sizes (P): stock 0.05 long along +Z from the shoulder; prod span 0.045 across X; pavise 0.030 wide by 0.046 tall by 0.004 thick, centred
at (0, 0.055, -0.024). Width at the prod about 0.05, the same as the soldier's spear-plus-shield.

### 2.3 `person_longbow`: the archer, at full draw

**Silhouette.** A hooded figure with a **tall curved stave** in front, taller than the archer, and the string pulled back into a V to the
cheek. From above, a quiver of pale fletchings over the right shoulder. Read at overview zoom: "a tall ( in front of a hooded figure". The
spear is a straight line held to one side; the bow is a curve held in front, in a paler wood, with the string catching the light.

**Pose.** Left arm straight forward holding the stave at the grip (y about 0.06, z about 0.035); right hand drawn back to the right cheek
(about (0.01, 0.082, 0.004)); the arrow runs from the cheek to the grip along +Z.

**Palette.** Tunic `TAG_CLOTH` as `_person`; a hood with shoulder cape in a muted green-brown `#55603e` (the one new colour; it keeps the
archer from reading as a monk); stave in pale yew `#c49a5a` (lighter than the spear's `#8a6a48`); string `#e8dfc8`; arrow shaft `#8a6a48`
with an iron head `#3f444c`; quiver `#6e5238` with fletchings `#efe6d0`.

**Owner's colour.** The mantle, plus (P) a short civ-coloured band at the quiver's mouth so the colour also shows from behind.

| Part | Builder calls | Triangles (approx.) |
|---|---|---|
| Legs, tunic, mantle, sash | 2 prism(5), 3 prism(7) | 30 + 63 = 93 |
| Arms: left straight, right bent back to the cheek | 4 pole | 48 |
| Head and hair, hood and cape | blob(7,4), blob(7,3), blob(7,3) | 98 |
| Stave: four pole segments on an arc (sagitta about 0.018) | 4 pole | 48 |
| String: two poles from the tips to the right hand | 2 pole | 24 |
| Arrow and head | pole, prism(4) cone | 12 + 4 = 16 |
| Back quiver, civ band, four fletchings | prism(6), prism(6) thin, 4 small box | 18 + 18 + 48 = 84 |
| **Total** | | **about 410; budget 450** |

Sizes (P): stave from y 0.018 to y 0.125 (about 1.1 times the figure); quiver 0.03 tall, slanted 20 degrees over the right shoulder.

### 2.4 What is not built

No second pose (spanning a crossbow, nocking an arrow), no animated loosing on the map, no tier variants (the prod is always iron; steel
kits do not change the bow), no mounted archer, no far-LOD variant (`person_far` stays the one silhouette for every person at regional
zoom).

## 3. Where they are seen, and the mix rule

### 3.1 One rule for every armed figure

Today the weapon choice is spread across four places and two files, each with its own little rule. This spec puts it in **one helper in
`world_builder.gd`**, called from all of them:

```
_arms_mesh(civ: int, seed: float, warrior: bool) -> String
```

`seed` is the person's own stable random number (`hash01` of the figure id, already computed in `_figures`), so each person keeps the same
weapon for the whole match. For the garrison and drill ground, which are drawn from a count and not from people, slot `i` of 6 uses the
stand-in seed `(i + 0.5) / 6` and `warrior = true` (they are professional soldiers).

The rule, in order (knights are decided before this, in the existing riding branch, and are not changed):

| Step | Condition | Mesh |
|---|---|---|
| 1 | gunpowder (WAR-12) known and `seed >= 0.7` | `person_gunner` |
| 2 | WAR-11 known and (`seed >= 0.7`, or the person is **not** a trained warrior) | the civilization's bow: `person_crossbow` or `person_longbow` |
| 3 | iron arms (WAR-08) known | `person_soldier` |
| 4 | otherwise | `person_spear` (with its tool tier, as today) |

Then the `_g` suffix where the mesh has one and the civilization has tailored clothing (unchanged code).

What this gives, as shares of the armed people you see:

| Civilization knows | Trained warriors | Called-up ordinary adults (the levy) |
|---|---|---|
| WAR-08 only | all soldiers | all soldiers |
| + WAR-11 | 70% soldiers, **30% bows** | **100% bows** |
| + WAR-11 + WAR-12 | 70% soldiers, 30% gunners | **70% bows**, 30% gunners |
| + WAR-10 | as above, except that on open ground, riding, `seed < 0.45` rides a knight (existing rule) | the same knight rule applies today (see Q4) |

**No costume party:** in any one civilization at most three foot looks ever appear together (spear-or-soldier, one bow style, gunner),
plus knights on open ground. Spear and soldier never mix (one look bit); crossbow and longbow never mix (one style per civilization,
§3.2); the gunner takes the same top band the bow had, so gunpowder **displaces** the bow from the trained men and thins it in the levy
rather than adding a fourth look.

### 3.2 Crossbow or longbow: one style per civilization

`_bow_mesh(civ)` returns `person_longbow` when `M7WorldData.hash01(civ, 0, 113) < 0.5`, else `person_crossbow` (P: the salt 113 is any
unused number). Stable for the whole match and the same on every machine. See Q1 for the alternatives.

### 3.3 Where each place gets its figures

| Place | Code today | With WAR-11 | With WAR-11 and WAR-12 |
|---|---|---|---|
| **Walking armed people** (warriors at home, raid and assault parties, occupiers) | `world_builder._figures`, the `fl & 256` branch | `_arms_mesh` per person | `_arms_mesh` per person |
| **Castle garrison** (castle stage 4, up to 6 guards in a ring) | `core_layout.gd` around line 660 and `world_builder._castle` around line 2177: all `person_soldier` | slots 4 and 5 bows, **facing outward** over the wall (yaw + PI/2 - ang; confirm in the capture), the rest soldiers | slots 4 and 5 gunners |
| **Drill ground** (up to 6 in two ranks of three) | `core_layout.gd` around line 755 and `world_builder._drill`: all `person_soldier` | slots 4 and 5 (back rank) bows, facing the hearth like the rest | slots 4 and 5 gunners |
| **Skirmish** (raid and assault fight, `skirmish_rig.gd`) | both sides all `person_spear` | a **missile rank**: a third of the shown fighters of a side whose civilization knows WAR-11 stand 0.06 behind their line, do not rush, and loose **one volley** of 3 to 5 arrow streaks at the clash point during the rush (the sim's "first-round volley"). They take the same fates (stand, fall, run) as their side. In reduced motion, no streaks | gunpowder side: the missile rank is gunners with a muzzle puff instead of streaks (P, later) |
| **Besieged wall** (new, last step) | nothing on the walls of a town under siege | up to **3 bowmen on the wall runs** either side of the breach face, at wall-top height (about `WALL_H * WALL_SCALE * sy`), facing out toward the besieger; only on a **viewer's own** besieged town with soldiers in post; never on a broken run | the same, gunners from slot 2 (P) |
| **Siege camp** (besieger) | engines, and since WAR-12 a cannon with two gunners | no change: the bows of an attacking host are the walking party figures | no change |

### 3.4 Budget

The walking figures only **swap a mesh**; no figure is added, so the 600-per-civilization cap (32 §6, A-6) is untouched. The garrison and
drill ground swap meshes inside their existing six slots. The skirmish missile rank comes out of the side's existing shown count
(`SIDE_MAX` 6, `ASSAULT_SIDE_MAX` 8), so a fight draws no more fighters; it adds one more MultiMesh per side and at most 5 streak quads.
The besieged-wall defenders add at most 3 static props per besieged own town, only while besieged. Triangle cost: a levy of 30 bowmen is
about 30 × 460 = 14,000 triangles, the same order as 30 soldiers.

## 4. Data and renderer plumbing

### 4.1 The look bit: in `IronFrame.CivLooks`, not `MatchFrame.CivLooks`

`MatchFrame.CivLooks` is a `ushort` per civilization and, with Knights (16384) and Gunpowder (32768) added in this session, every bit is
used. `IronFrame.CivLooks` is an `int[]` per civilization made for exactly this overflow ("small-prop looks of the Iron Age that the 16
bits of MatchFrame.CivLooks have no room for"); it uses bits 1 to 16 and has 27 free. It is filled whenever `IronChain` is on, and
**`FiefChain` turns `IronChain` on** (`LivingEconomyModel.Fief.cs` on `codex/e5-fief`), so it is live in every match where WAR-11 can be
completed. `Game.cs` already copies it to Godot as `ironLooks` (line 871) and `world_data.has_iron_look` already reads it.

Changes (each one or two lines):

| File | Change |
|---|---|
| `src/CivSim.Sim/View/IronFrame.cs` | `LookCrossbows = 32`; widen the comment to "Iron and later ages". |
| `src/CivSim.Sim/View/FrameBuilder.Iron.cs` | add `("WAR-11", IronFrame.LookCrossbows)` to the `looks` table. |
| `game/Renderer/LowPoly3D/Scripts/world_data.gd` | `const IRON_CROSSBOWS := 32  # WAR-11 Crossbows & Longbows`; `--looks-debug` fills `iron_looks` with **63** instead of 31. |
| `game/Scripts/Game.cs` | none. |

Alternatives considered: (a) widen `CivLook` to `uint`: cleaner long term, but it changes a public frame type, `Game.cs` conversion and
any frame dumps, and it touches the two files another agent is editing now; worth doing as its own deliberate change later, not for one
figure. (b) a `CivLooks` array on `FiefFrame`: right in spirit, but `FiefFrame` exists only on `codex/e5-fief` and has no Godot bridge yet.
If (b) is built later, the bit can move there in one commit.

Determinism: the view is not part of the sim checksum, so the goldens should not move; the build steps below still run them.

### 4.2 Renderer

| File | Change |
|---|---|
| `art_fief_people.gd` | `_crossbow(kit, garment)` and `_longbow(kit, garment)`; register `person_crossbow`, `person_crossbow_g`, `person_longbow`, `person_longbow_g` in `build`. Builder seeds 1606 to 1609. Header list updated. |
| `mesh_kit.gd` | none: `art_fief_people.gd` is already in `ART_PASSES` (that list, not `LowPoly3DView.gd`, is where art passes are registered and preloaded). |
| Materials | none: names begin with `person`, so the batcher (`batcher.gd` line 81) and `world_builder.gd` (line 556) give them the figure material, the instance civ colour and the walk glide automatically. |
| `world_builder.gd` | `_bow_mesh(civ)` and `_arms_mesh(civ, seed, warrior)`; the armed branch of `_figures` calls it (replacing the soldier and gunner lines just added for WAR-12); `_drill` and `_castle` call it per slot; later, wall defenders in `_stone_walls` when a siege dictionary is passed and the town is the viewer's. |
| `core_layout.gd` | the garrison loop and the drill-ground loop call `wb._arms_mesh`. The drill-ground layout record keeps `"mesh": "person_soldier"` as the group's name so `check_core_layout.py` is unaffected (or record a list; check the script first). |
| `skirmish_rig.gd` | a `"missile"` MultiMesh per side, the side's bow mesh from `_bow_mesh`, a streak pass reusing the spark quad with a thin stretched transform. |
| `LowPoly3DView.gd` | `show_fief_gallery`: add a levy row (see §5 step 1). |
| `Game.Scenarios.cs` | `fief-gallery` already shoots the figure row; add a shot name prefix `levy` so `--fief-only=levy` photographs just that row. |

## 5. Build order

Wait for the WAR-10 and WAR-12 change now in progress in `world_builder.gd`, `world_data.gd`, `ViewTypes.cs` and `FrameBuilder.cs` to land
before step 2; step 1 touches none of those files.

| Step | Work | Acceptance |
|---|---|---|
| **1. Meshes and gallery** | the four meshes in `art_fief_people.gd`; a gallery row: `person_spear`, `person_soldier`, `person_crossbow`, `person_longbow`, `person_gunner`, then the `_g` pair, at scale 2.4 | `--scenario=fief-gallery --fief-only=levy` capture (add `--always-on-top` on a machine in use, 32 §10). In the still: the crossbow and longbow tops line up with the soldier's helmet within 5%; at overview zoom with no labels a viewer can tell spear, crossbow, longbow and gunner apart; the owner's colour shows on the mantle and on the pavise or quiver band; triangle count per mesh printed once in the log (vertex count / 3) is within §2 budgets. |
| **2. Look bit** | `IronFrame.LookCrossbows`, the `FrameBuilder.Iron.cs` row, `IRON_CROSSBOWS` and the debug fill of 63 | `dotnet build` and `dotnet test` green; the three goldens in CLAUDE.md unchanged; a frame dump from an Iron match shows bit 32 clear for every civilization. |
| **3. Walking figures** | `_bow_mesh`, `_arms_mesh`, and the armed branch of `_figures` uses it | `--scenario=looks --looks-debug` capture of a raid party: bows, soldiers and gunners all visible, no civilization showing both bow styles; a debug count of props by mesh name over one frame gives bow shares within 5 points of the §3.1 table; without `--looks-debug`, an Iron match capture is pixel-identical to before (A-7). |
| **4. Garrison and drill ground** | `_castle`, `_drill`, and the two `core_layout.gd` loops use `_arms_mesh` | `--scenario=war-village --dev-soldiers=6 --looks-debug` capture: castle guards 4 soldiers and 2 outward-facing bows; drill ground front rank soldiers, back rank one soldier and two bows; with gunpowder the two become gunners. `check_core_layout.py` passes. |
| **5. Skirmish volley** | missile rank and volley in `skirmish_rig.gd` | `--scenario=skirmish --looks-debug` stills at 0.5 s and 1.5 s: the bow rank stands behind its line, streaks arc to the clash in the rush and stop after it; reduced-motion run shows no streaks; total fighters per side unchanged. |
| **6. Besieged wall** | wall defenders in `_stone_walls` for an own besieged town | `--scenario=war-village --dev-soldiers=6 --dev-siege=500 --looks-debug`: up to three bowmen on whole wall runs facing the besieger, none on a broken run, none on a rival's besieged town; at `--dev-siege=0` the broken runs carry none. |

Steps 1 and 2 can go in one commit; each later step is its own commit with its capture.

## 6. Open questions, each with a recommended answer

**Q1. Crossbow, longbow, or both?** The sim has one good (G-CROSSBOW) and one combat row. Options: (a) crossbows only, longbow mesh
built for later; (b) one style per civilization by a stable hash, half and half; (c) both mixed in every army.
**Recommended: (b).** The project is called "Crossbows & Longbows", the combat row is one "levy ranged weapon", and one style per
civilization gives each people a recognisable look without a costume party. If you prefer the strictest reading of "draw only what the
sim has", choose (a); it is a one-line change in `_bow_mesh`.

**Q2. Where does the look bit live?** **Recommended: `IronFrame.CivLooks` bit 32** (§4.1). Widening `CivLook` to 32 bits is the better
long-term home and can be done later on its own.

**Q3. Should the bow follow the stock of crossbows rather than the project?** Doc 32 rule 1 would prefer "the figure holds the item".
Today nobody holds a crossbow in the sim. **Recommended: project look now (as knights and gunners do); when the sim implements the WAR-11
combat row, it should mark each figure that fights with a crossbow with a new figure flag (`FigureFlags` has bits 2048 to 32768 free
below the carry field), and `_arms_mesh` should switch to that flag.** No mesh changes then.

**Q4. Should only trained warriors ride as knights?** Today any armed rider with `seed < 0.45` on open ground becomes a knight, so a
called-up farmer can appear as a knight in steel. **Recommended: yes, add `fl & 128` (warrior) to the knight test** when the in-progress
WAR-10 change lands. A related finding: a civilization with both chariots and knights still puts the `0.45 <= seed < 0.7` band of raiders
in chariots, a Bronze look in a Fief army. **Recommended: chariots stop once a civilization has Knights.** Both are small edits to the
other agent's code, so they are listed here rather than made.

**Q5. Tailored (`_g`) variants?** **Recommended: build them for the two levy figures now** (nearly every E5 civilization has FIB-04), and
add `person_gunner_g` in the same pass, since the gunner is currently the only bare-legged figure in a tailored crowd.

**Q6. The short E5 figures.** By the code, the monk, pilgrim and gunner stand about 0.063 to 0.087 tall against 0.097 for the villagers,
because they skip the torso lift of `_person`. **Recommended: confirm in the step-1 gallery row with the soldier beside them, and if the
difference shows, lift them in a separate small commit.**

**Q7. Wall defenders on a rival's besieged town?** The garrison size of another civilization is private (`IronFrame.Soldiers` is own
settlements only). **Recommended: own towns only.** A besieger sees the walls and the breach, not who stands behind them.

**Q8. Should the levy name collide with the "Raise the levy" decree?** The decree (doc 22 §4.3) gathers a host of warriors plus half the
other adults; those called-up adults are exactly the figures that will carry bows. **Recommended: keep the word "levy"**; it means the
same people in both places, and the art makes the decree's host visible.

**Also noticed, for the agent editing `world_data.gd`:** `--looks-debug` fills `civ_looks` with 16383, which leaves out the new Knights
(16384) and Gunpowder (32768) bits; it should be 65535 for the debug captures to show them.
