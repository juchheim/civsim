## Meshes for the projects that change how a civilization's land and people look (MatchFrame.CivLooks): the furrowed and the
## resting field, the ox team that draws the ard, plank boats and the sailing ship, the ox cart, the chariot (under a raider and
## parked in the village), the stepped temple, the chief's hall and the bales of fleece. See looks_rig.gd for where they go.
##
## Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.1 tall). Orientation follows each mesh's family:
## things that glide under a person (person_*) face +Z like people; rig meshes (ox teams, carts) face +X like animals and dogs;
## village decor faces +Z, toward the village centre.
class_name M7LooksKit
extends RefCounted

const PLANK_LOW := Color("#8a6238")
const PLANK_UP := Color("#b88a56")
const PITCH := Color("#3a2c1f")
const SAIL_CREAM := Color("#e8dcc0")
const FUR := Color("#e6dcc4")
const PARKA := Color("#7a5a3a")
const LEGGING := Color("#5c4630")
const BOOT := Color("#2f2318")
const ADOBE := Color("#c9a77a")
const ADOBE_DARK := Color("#a98a5e")
const GOLD := Color("#c9973c")
const DARK := Color("#2a2119")
const OX_DARK := Color("#4a3426")
const OX_DUN := Color("#a88458")
const HORSE_BAY := Color("#7a4a2a")
const HORSE_DARK := Color("#4a3020")
const FLEECE := Color("#efe8d6")

static func build(kit: M7MeshKit) -> void:
	_fields(kit)
	_ships(kit)
	_chariot(kit)
	_ard_team(kit)
	_cart(kit)
	_decor(kit)

# ------------------------------------------------------------------ shared pieces

## Runs the builder with its transform set to `xf`, then puts the old one back.
static func _with(b: M7MeshBuilder, xf: Transform3D) -> Transform3D:
	var save := b.xf
	b.xf = save * xf
	return save

## A wheel standing in a vertical plane around the axle direction `axis` (Vector3.RIGHT or Vector3.BACK). `solid` is the
## plank disc of the first wheel; otherwise it has spokes.
static func _wheel(b: M7MeshBuilder, c: Vector3, r: float, axis: Vector3, solid: bool, col: Color) -> void:
	var u := Vector3.UP
	var v := u.cross(axis).normalized()
	var n := 10
	var t := r * 0.13
	for i in n:
		var a0 := TAU * i / n
		var a1 := TAU * (i + 1) / n
		var p0 := c + (v * cos(a0) + u * sin(a0)) * r
		var p1 := c + (v * cos(a1) + u * sin(a1)) * r
		if solid:
			b.tri(c + axis * t, p0 + axis * t, p1 + axis * t, col, c + axis * (t + 1.0))
			b.tri(c - axis * t, p0 - axis * t, p1 - axis * t, col.darkened(0.12), c - axis * (t + 1.0))
			b.quad(p0 + axis * t, p1 + axis * t, p1 - axis * t, p0 - axis * t, col.darkened(0.25), c)
		else:
			b.pole(p0, p1, r * 0.09, col)
			if i % 2 == 0:
				b.pole(c, p0, r * 0.05, col)
	b.pole(c - axis * r * 0.22, c + axis * r * 0.22, r * 0.17, col.darkened(0.3))

## A tiny standing person (~0.1 tall) at `pos`, facing along `yaw` (0 = +Z). Used inside ox teams, carts and chariots.
static func _mini_person(b: M7MeshBuilder, pos: Vector3, yaw: float, spear: bool = false, stick: bool = false) -> void:
	var save := _with(b, Transform3D(Basis(Vector3.UP, yaw), pos))
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.prism(Vector3(-0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, LEGGING, LEGGING)
	b.prism(Vector3(0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, LEGGING, LEGGING)
	b.tag = M7MeshBuilder.TAG_CLOTH
	b.prism(Vector3(0, 0.034, 0), 0.018, 0.015, 0.036, 7, Color("#8a6a48"), Color("#8a6a48"))
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.054, 0), 0.0215, 0.009, 0.022, 7, Color.WHITE, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.pole(Vector3(-0.02, 0.064, 0), Vector3(-0.024, 0.04, 0.006), 0.0045, M7MeshKit.SKIN)
	if spear:
		b.pole(Vector3(0.02, 0.064, 0), Vector3(0.03, 0.07, 0.03), 0.0045, M7MeshKit.SKIN)
		b.pole(Vector3(0.03, 0.03, 0.026), Vector3(0.034, 0.15, 0.036), 0.0026, M7MeshKit.WOOD_LIGHT)
		b.prism(Vector3(0.034, 0.15, 0.036), 0.005, 0.0, 0.016, 4, M7MeshKit.STONE_DARK, M7MeshKit.STONE_DARK)
	else:
		b.pole(Vector3(0.02, 0.064, 0), Vector3(0.026, 0.042, 0.012), 0.0045, M7MeshKit.SKIN)
	if stick:
		b.pole(Vector3(0.03, 0.0, 0.03), Vector3(0.03, 0.09, 0.02), 0.0026, M7MeshKit.WOOD_LIGHT)
	b.blob(Vector3(0, 0.084, 0), Vector3(0.0125, 0.013, 0.0125), 7, 4, M7MeshKit.SKIN, 0.0)
	b.blob(Vector3(0, 0.089, -0.003), Vector3(0.0128, 0.009, 0.012), 7, 3, M7MeshKit.HAIR, 0.0)
	b.xf = save

## An ox, facing +X, ~0.085 long: the legs swap with `phase` (0 or 1) so a walking team reads as walking.
static func _ox(b: M7MeshBuilder, p: Vector3, phase: int, coat: Color) -> void:
	var legc := Color("#2f241b")
	var s := 0.006 if phase == 0 else -0.006
	b.prism(p + Vector3(-0.03 - s, 0, -0.012), 0.0055, 0.0065, 0.04, 4, legc, legc)
	b.prism(p + Vector3(-0.03 + s, 0, 0.012), 0.0055, 0.0065, 0.04, 4, legc, legc)
	b.prism(p + Vector3(0.028 + s, 0, -0.012), 0.0055, 0.0065, 0.04, 4, legc, legc)
	b.prism(p + Vector3(0.028 - s, 0, 0.012), 0.0055, 0.0065, 0.04, 4, legc, legc)
	b.blob(p + Vector3(0, 0.052, 0), Vector3(0.05, 0.024, 0.021), 7, 4, coat, 0.06)
	b.blob(p + Vector3(0.024, 0.066, 0), Vector3(0.022, 0.017, 0.018), 6, 3, coat, 0.06)
	b.blob(p + Vector3(0.056, 0.046, 0), Vector3(0.016, 0.013, 0.012), 6, 3, coat.lightened(0.06), 0.04)
	b.pole(p + Vector3(0.058, 0.056, -0.008), Vector3(p.x + 0.07, p.y + 0.07, p.z - 0.02), 0.0025, M7MeshKit.CREAM)
	b.pole(p + Vector3(0.058, 0.056, 0.008), Vector3(p.x + 0.07, p.y + 0.07, p.z + 0.02), 0.0025, M7MeshKit.CREAM)
	b.pole(p + Vector3(-0.05, 0.058, 0), p + Vector3(-0.058, 0.03, 0.0), 0.0022, legc)

## A horse facing +Z (for the chariot), ~0.08 long, at ground point p. `coat` and the dark mane/tail/legs.
static func _horse(b: M7MeshBuilder, p: Vector3, coat: Color, dark: Color, stride: float) -> void:
	for lz in [-0.026, 0.024]:
		for lx in [-0.008, 0.008]:
			var sw: float = stride * (1.0 if (lx > 0.0) == (lz > 0.0) else -1.0)
			b.pole(p + Vector3(lx, 0.034, lz), p + Vector3(lx, 0.0, lz + sw), 0.0034, dark)
	b.blob(p + Vector3(0, 0.046, 0), Vector3(0.0125, 0.017, 0.038), 7, 4, coat, 0.04)
	b.pole(p + Vector3(0, 0.054, 0.026), p + Vector3(0, 0.078, 0.042), 0.0065, coat)
	b.blob(p + Vector3(0, 0.082, 0.05), Vector3(0.0075, 0.0085, 0.0145), 6, 3, coat.lightened(0.05), 0.0)
	b.pole(p + Vector3(0, 0.088, 0.036), p + Vector3(0, 0.07, 0.024), 0.0034, dark)
	b.pole(p + Vector3(0, 0.05, -0.034), p + Vector3(0, 0.022, -0.052), 0.004, dark)

# ------------------------------------------------------------------ fields (AGR-05, AGR-07)

static func _hex_ring(r: float) -> Array:
	var hexp := []
	for i in 6:
		hexp.append(M7WorldData.corner(i) / M7WorldData.R * r)
	return hexp

static func _soil_hex(b: M7MeshBuilder, r: float, col: Color) -> void:
	var hexp := _hex_ring(r)
	b.tag = M7MeshBuilder.TAG_PLAIN
	for i in 6:
		var p0: Vector2 = hexp[i]
		var p1: Vector2 = hexp[(i + 1) % 6]
		b.tri(Vector3(0, 0.004, 0), Vector3(p0.x, 0.004, p0.y), Vector3(p1.x, 0.004, p1.y), col, Vector3(0, -1, 0))

static func _fields(kit: M7MeshKit) -> void:
	var fr := M7WorldData.R * 0.84
	# ard-ploughed field: straight, deep furrows and broad ridges, a headland strip and boundary stones at the corners
	var b := kit._b(341)
	_soil_hex(b, fr, M7MeshKit.SOIL.darkened(0.12))
	var rows := 9
	for r in rows:
		var z := -fr + (r + 0.5) * (2.0 * fr / rows)
		var hw := M7MeshKit._hex_half_width(z, fr) - 0.03
		if hw < 0.05:
			continue
		var hh := 0.024
		var wdt := 0.026
		b.tag = M7MeshBuilder.TAG_CROP
		var a0 := Vector3(-hw, 0.004, z - wdt)
		var a1 := Vector3(hw, 0.004, z - wdt)
		var c0 := Vector3(-hw, 0.004 + hh, z)
		var c1 := Vector3(hw, 0.004 + hh, z)
		var d0 := Vector3(-hw, 0.004, z + wdt)
		var d1 := Vector3(hw, 0.004, z + wdt)
		b.quad(a0, a1, c1, c0, M7MeshKit.SOIL.lightened(0.05), Vector3(0, 0, z + 0.05))
		b.quad(d0, d1, c1, c0, M7MeshKit.SOIL.darkened(0.05), Vector3(0, 0, z - 0.05))
		b.tri(a0, c0, d0, M7MeshKit.SOIL, Vector3(0.0, 0.0, z))
		b.tri(a1, c1, d1, M7MeshKit.SOIL, Vector3(0.0, 0.0, z))
	b.tag = M7MeshBuilder.TAG_PLAIN
	for i in 6:
		var corner: Vector2 = _hex_ring(fr * 0.97)[i]
		b.blob(Vector3(corner.x, 0.008, corner.y), Vector3(0.012, 0.01, 0.011), 5, 3, M7MeshKit.STONE, 0.1, M7MeshKit.STONE_DARK)
	kit.meshes["field_ard"] = b.commit()

	# resting field: old furrows worn flat, weeds and grass coming in, rows of dung heaps spread for the next sowing
	b = kit._b(343)
	_soil_hex(b, fr, Color("#86694a"))
	for r in 8:
		var z := -fr + (r + 0.5) * (2.0 * fr / 8.0)
		var hw := M7MeshKit._hex_half_width(z, fr) - 0.04
		if hw < 0.05:
			continue
		b.box(Vector3(0, 0.006, z), Vector3(hw * 2.0, 0.004, 0.02), Color("#7a5f42"))
	b.tag = M7MeshBuilder.TAG_GRASS
	for i in 11:
		var px := b.rnds() * 0.36
		var pz := b.rnds() * 0.34
		if absf(px) > M7MeshKit._hex_half_width(pz, fr) - 0.08:
			continue
		b.blob(Vector3(px, 0.006, pz), Vector3(0.06 + 0.05 * b.rnd(), 0.012, 0.045 + 0.04 * b.rnd()), 6, 2, M7MeshKit.GRASS.lerp(M7MeshKit.DRYGRASS, b.rnd()), 0.1)
	b.tag = M7MeshBuilder.TAG_PLAIN
	for row in 2:
		for i in 5:
			var px := -0.28 + i * 0.14 + 0.04 * b.rnds()
			var pz := (-0.15 if row == 0 else 0.17) + 0.03 * b.rnds()
			if absf(px) > M7MeshKit._hex_half_width(pz, fr) - 0.07:
				continue
			b.blob(Vector3(px, 0.006, pz), Vector3(0.026, 0.014, 0.022), 5, 3, Color("#4a3626"), 0.12, Color("#3a2a1c"))
	kit.meshes["field_fallow"] = b.commit()

# ------------------------------------------------------------------ boats (SEA-05 plank boat, SEA-06 sail)

## A planked hull along Z, centred, `length` long. Lower and upper strakes are two colours; the ends sweep up into posts.
static func _plank_hull(b: M7MeshBuilder, length: float, beam: float, depth: float) -> void:
	var n := 9
	var half := length * 0.5
	var st := []
	for i in n + 1:
		var u := float(i) / n
		var z := lerpf(-half, half, u)
		var e := absf(u * 2.0 - 1.0)
		var hw := beam * 0.5 * (1.0 - pow(e, 2.4) * 0.92)
		var rise := pow(e, 2.2) * depth * 1.5
		st.append([z, hw, rise])
	for i in n:
		var s0: Array = st[i]
		var s1: Array = st[i + 1]
		for sx in [-1.0, 1.0]:
			var keel0 := Vector3(0, 0.003 + float(s0[2]) * 0.3, float(s0[0]))
			var keel1 := Vector3(0, 0.003 + float(s1[2]) * 0.3, float(s1[0]))
			var chine0 := Vector3(sx * float(s0[1]) * 0.66, depth * 0.4 + float(s0[2]) * 0.55, float(s0[0]))
			var chine1 := Vector3(sx * float(s1[1]) * 0.66, depth * 0.4 + float(s1[2]) * 0.55, float(s1[0]))
			var gun0 := Vector3(sx * float(s0[1]), depth + float(s0[2]), float(s0[0]))
			var gun1 := Vector3(sx * float(s1[1]), depth + float(s1[2]), float(s1[0]))
			var inside := Vector3(0, depth * 0.5, (float(s0[0]) + float(s1[0])) * 0.5)
			b.quad(keel0, keel1, chine1, chine0, PLANK_LOW, inside)
			b.quad(chine0, chine1, gun1, gun0, PLANK_UP, inside)
		# the open hull: a dark floor between the gunwales
		var f0: float = float(s0[1]) * 0.6
		var f1: float = float(s1[1]) * 0.6
		b.quad(Vector3(-f0, depth * 0.55 + float(s0[2]) * 0.55, float(s0[0])), Vector3(f0, depth * 0.55 + float(s0[2]) * 0.55, float(s0[0])),
			Vector3(f1, depth * 0.55 + float(s1[2]) * 0.55, float(s1[0])), Vector3(-f1, depth * 0.55 + float(s1[2]) * 0.55, float(s1[0])), PITCH, Vector3(0, 1, 0))
		# the civilization's colour painted along the gunwale
		b.tag = M7MeshBuilder.TAG_CIV
		for sx in [-1.0, 1.0]:
			var g0 := Vector3(sx * float(s0[1]), depth + float(s0[2]) + 0.003, float(s0[0]))
			var g1 := Vector3(sx * float(s1[1]), depth + float(s1[2]) + 0.003, float(s1[0]))
			b.pole(g0, g1, 0.0042, Color.WHITE)
		b.tag = M7MeshBuilder.TAG_PLAIN
	# stem and stern posts
	for end in [-1.0, 1.0]:
		var z: float = end * half
		var top: float = depth + depth * 1.5
		b.pole(Vector3(0, top - 0.004, z), Vector3(0, top + 0.03, z + end * 0.014), 0.0048, M7MeshKit.WOOD)
	# thwarts
	for k in 3:
		var z := lerpf(-half * 0.5, half * 0.5, k / 2.0)
		var e := absf(z / half)
		var hw := beam * 0.5 * (1.0 - pow(e, 2.4) * 0.92)
		b.box(Vector3(0, depth * 0.9, z), Vector3(hw * 2.0, 0.004, 0.009), M7MeshKit.WOOD_LIGHT)

static func _ships(kit: M7MeshKit) -> void:
	# tier 5: plank boat with three oars a side
	var b := kit._b(750)
	_plank_hull(b, 0.3, 0.078, 0.032)
	for k in 3:
		var z := lerpf(-0.07, 0.07, k / 2.0)
		for sx in [-1.0, 1.0]:
			b.pole(Vector3(sx * 0.03, 0.04, z), Vector3(sx * 0.1, -0.006, z - 0.026), 0.0026, M7MeshKit.WOOD_LIGHT)
			b.box(Vector3(sx * 0.1, -0.004, z - 0.026), Vector3(0.016, 0.003, 0.012), M7MeshKit.WOOD_LIGHT)
	kit.meshes["person_boat_5"] = b.commit()

	# tier 6: the same hull under a square sail in the civilization's colour with a pale band; oars shipped, a steering oar astern
	b = kit._b(751)
	_plank_hull(b, 0.34, 0.088, 0.036)
	b.pole(Vector3(0, 0.03, 0.02), Vector3(0, 0.27, 0.02), 0.0042, M7MeshKit.WOOD)
	b.pole(Vector3(-0.085, 0.245, 0.02), Vector3(0.085, 0.245, 0.02), 0.0034, M7MeshKit.WOOD)
	var cols := 4
	var rws := 3
	for r in rws:
		var y0 := 0.24 - r * 0.045
		var y1 := y0 - 0.045
		for c in cols:
			var x0 := -0.08 + c * 0.04
			var x1 := x0 + 0.04
			var bz0 := _belly(x0, 0.08, y0, 0.24, 0.1)
			var bz1 := _belly(x1, 0.08, y0, 0.24, 0.1)
			var bz2 := _belly(x1, 0.08, y1, 0.24, 0.1)
			var bz3 := _belly(x0, 0.08, y1, 0.24, 0.1)
			b.tag = M7MeshBuilder.TAG_PLAIN if r == 1 else M7MeshBuilder.TAG_CIV
			b.quad(Vector3(x0, y0, 0.02 + bz0), Vector3(x1, y0, 0.02 + bz1), Vector3(x1, y1, 0.02 + bz2), Vector3(x0, y1, 0.02 + bz3),
				SAIL_CREAM if r == 1 else Color.WHITE, Vector3(0, 0.17, -0.3))
	b.tag = M7MeshBuilder.TAG_PLAIN
	# stays to bow and stern, sheet to the quarter
	b.pole(Vector3(0, 0.26, 0.02), Vector3(0, 0.06, 0.17), 0.0014, Color("#b9a68a"))
	b.pole(Vector3(0, 0.26, 0.02), Vector3(0, 0.06, -0.17), 0.0014, Color("#b9a68a"))
	b.pole(Vector3(0.04, 0.03, -0.14), Vector3(0.056, -0.012, -0.19), 0.0034, M7MeshKit.WOOD_LIGHT)
	b.box(Vector3(0.058, -0.008, -0.192), Vector3(0.006, 0.026, 0.014), M7MeshKit.WOOD_LIGHT)
	kit.meshes["person_boat_6"] = b.commit()
	for tier in [5, 6]:
		kit.meshes["boat_%d" % tier] = kit.meshes["person_boat_%d" % tier]

## How far a sail bellies forward at (x, y): most in the middle, none at the edges.
static func _belly(x: float, half_w: float, y: float, top: float, height: float) -> float:
	var u := clampf(1.0 - absf(x) / half_w, 0.0, 1.0)
	var v := clampf((top - y) / (height * 1.2), 0.0, 1.0)
	return 0.034 * sin(u * PI * 0.5) * sin(clampf(v * 1.1, 0.0, 1.0) * PI * 0.9)

# ------------------------------------------------------------------ chariot (WAR-06)

## The chariot box, wheels and shield, floor at the origin, facing +Z. With `with_horses` a pair stands ahead; without, the
## pole rests on a fork (parked in the village).
static func _chariot_body(b: M7MeshBuilder, with_horses: bool, stride: float) -> void:
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.box(Vector3(0, 0.026, 0), Vector3(0.05, 0.004, 0.044), M7MeshKit.WOOD_LIGHT)
	b.box(Vector3(0, 0.038, 0.021), Vector3(0.05, 0.024, 0.004), M7MeshKit.WOOD)
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * 0.0245, 0.036, 0.0), Vector3(0.004, 0.02, 0.044), M7MeshKit.WOOD)
		_wheel(b, Vector3(sx * 0.034, 0.02, -0.004), 0.02, Vector3.RIGHT, false, M7MeshKit.WOOD_LIGHT)
	b.pole(Vector3(-0.034, 0.02, -0.004), Vector3(0.034, 0.02, -0.004), 0.003, M7MeshKit.WOOD)
	b.tag = M7MeshBuilder.TAG_CIV
	var save := _with(b, Transform3D(Basis(Vector3.RIGHT, PI * 0.5), Vector3(0, 0.04, 0.0235)))
	b.prism(Vector3.ZERO, 0.0115, 0.0115, 0.003, 8, Color.WHITE, Color.WHITE)
	b.xf = save
	b.tag = M7MeshBuilder.TAG_PLAIN
	if with_horses:
		b.pole(Vector3(0, 0.03, 0.02), Vector3(0, 0.058, 0.13), 0.0034, M7MeshKit.WOOD)
		b.pole(Vector3(-0.026, 0.066, 0.124), Vector3(0.026, 0.066, 0.124), 0.0034, M7MeshKit.WOOD)
		_horse(b, Vector3(-0.017, 0, 0.11), HORSE_BAY, HORSE_DARK, stride)
		_horse(b, Vector3(0.017, 0, 0.11), HORSE_DARK.lightened(0.1), HORSE_DARK, -stride)
	else:
		b.pole(Vector3(0, 0.03, 0.02), Vector3(0, 0.07, 0.12), 0.0034, M7MeshKit.WOOD)
		b.pole(Vector3(-0.008, 0.0, 0.106), Vector3(0, 0.054, 0.11), 0.003, M7MeshKit.WOOD)
		b.pole(Vector3(0.008, 0.0, 0.106), Vector3(0, 0.054, 0.11), 0.003, M7MeshKit.WOOD)

static func _chariot(kit: M7MeshKit) -> void:
	# under a raider: the crew stands in it with a spear
	var b := kit._b(760)
	_chariot_body(b, true, 0.006)
	_mini_person(b, Vector3(0, 0.028, -0.004), 0.0, true)
	kit.meshes["person_chariot"] = b.commit()
	# parked in the village, shaft resting on a fork
	b = kit._b(761)
	_chariot_body(b, false, 0.0)
	kit.meshes["decor_chariot"] = b.commit()

# ------------------------------------------------------------------ ard team (AGR-05), ox cart (TRN-05); rig meshes face +X

static func _ard_team(kit: M7MeshKit) -> void:
	for phase in 2:
		var b := kit._b(770 + phase)
		_ox(b, Vector3(0.17, 0, -0.028), phase, OX_DARK)
		_ox(b, Vector3(0.17, 0, 0.028), 1 - phase, OX_DUN)
		# the yoke across their necks and the draw-beam down to the ard
		b.pole(Vector3(0.2, 0.074, -0.044), Vector3(0.2, 0.074, 0.044), 0.0038, M7MeshKit.WOOD)
		b.pole(Vector3(0.2, 0.072, 0.0), Vector3(0.0, 0.016, 0.0), 0.0036, M7MeshKit.WOOD)
		# the ard: a share dragging in the soil, a handle for the ploughman
		b.pole(Vector3(0.0, 0.016, 0.0), Vector3(-0.012, 0.0, 0.0), 0.0042, M7MeshKit.STONE_DARK)
		b.pole(Vector3(0.012, 0.014, 0.0), Vector3(-0.045, 0.074, 0.004), 0.0032, M7MeshKit.WOOD_LIGHT)
		_mini_person(b, Vector3(-0.07, 0.0, 0.004), PI * 0.5, false, false)
		kit.meshes["ard_team_%s" % ("a" if phase == 0 else "b")] = b.commit()

static func _cart(kit: M7MeshKit) -> void:
	for phase in 2:
		var b := kit._b(775 + phase)
		_ox(b, Vector3(0.2, 0, -0.028), phase, OX_DUN)
		_ox(b, Vector3(0.2, 0, 0.028), 1 - phase, OX_DARK)
		b.pole(Vector3(0.23, 0.074, -0.044), Vector3(0.23, 0.074, 0.044), 0.0038, M7MeshKit.WOOD)
		b.pole(Vector3(0.23, 0.072, 0.0), Vector3(0.04, 0.04, 0.0), 0.0036, M7MeshKit.WOOD)
		# the bed with low boards, solid plank wheels, the load, and a hide awning in the civilization's colour over the back
		b.box(Vector3(-0.02, 0.036, 0), Vector3(0.12, 0.006, 0.066), M7MeshKit.WOOD_LIGHT)
		b.box(Vector3(-0.02, 0.048, -0.0345), Vector3(0.12, 0.018, 0.004), M7MeshKit.WOOD)
		b.box(Vector3(-0.02, 0.048, 0.0345), Vector3(0.12, 0.018, 0.004), M7MeshKit.WOOD)
		b.box(Vector3(-0.0785, 0.048, 0.0), Vector3(0.004, 0.018, 0.066), M7MeshKit.WOOD)
		for sz in [-1.0, 1.0]:
			_wheel(b, Vector3(-0.025, 0.026, sz * 0.043), 0.026, Vector3.BACK, true, M7MeshKit.WOOD_LIGHT.darkened(0.08))
		b.pole(Vector3(-0.025, 0.026, -0.043), Vector3(-0.025, 0.026, 0.043), 0.0032, M7MeshKit.WOOD)
		b.blob(Vector3(0.012, 0.056, -0.014), Vector3(0.014, 0.012, 0.012), 5, 3, Color("#c7b08a"), 0.1)
		b.blob(Vector3(0.016, 0.056, 0.014), Vector3(0.013, 0.011, 0.012), 5, 3, Color("#b99a72"), 0.1)
		b.prism(Vector3(-0.002, 0.042, 0.0), 0.011, 0.014, 0.022, 6, Color("#b5603c"), Color("#7a3a22"))
		b.tag = M7MeshBuilder.TAG_CIV
		b.quad(Vector3(-0.075, 0.056, -0.03), Vector3(-0.03, 0.056, -0.03), Vector3(-0.03, 0.084, 0.0), Vector3(-0.075, 0.084, 0.0), Color.WHITE, Vector3(-0.05, 0.04, 0.0))
		b.quad(Vector3(-0.075, 0.056, 0.03), Vector3(-0.03, 0.056, 0.03), Vector3(-0.03, 0.084, 0.0), Vector3(-0.075, 0.084, 0.0), Color.WHITE, Vector3(-0.05, 0.04, 0.0))
		b.tag = M7MeshBuilder.TAG_PLAIN
		# the driver walks at the near ox's flank with a goad
		_mini_person(b, Vector3(0.14, 0.0, 0.06), PI * 0.5, false, true)
		kit.meshes["ox_cart_%s" % ("a" if phase == 0 else "b")] = b.commit()

# ------------------------------------------------------------------ village decor: temple, chief's hall, fleece bales

static func _decor(kit: M7MeshKit) -> void:
	# temple (BEL-07): three mud-brick terraces, a stair up the front (+Z), a shrine under a glazed roof, a brazier and pennants
	var b := kit._b(780)
	var tiers := [[0.118, 0.104, 0.0, 0.05], [0.088, 0.076, 0.05, 0.047], [0.058, 0.05, 0.097, 0.04]]
	for t in tiers:
		b.prism(Vector3(0, float(t[2]), 0), float(t[0]), float(t[1]), float(t[3]), 4, ADOBE, ADOBE.lightened(0.12), PI * 0.25)
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.045, 0), 0.1086, 0.1086, 0.007, 4, Color.WHITE, Color.WHITE, PI * 0.25)
	b.prism(Vector3(0, 0.093, 0), 0.0795, 0.0795, 0.007, 4, Color.WHITE, Color.WHITE, PI * 0.25)
	b.tag = M7MeshBuilder.TAG_PLAIN
	for i in 9:
		var top := 0.137 * (i + 1) / 9.0
		b.box(Vector3(0, top * 0.5, 0.1 - i * 0.0063), Vector3(0.03, top, 0.0067), ADOBE_DARK, ADOBE.lightened(0.15))
	b.box(Vector3(0, 0.154, 0), Vector3(0.062, 0.028, 0.056), ADOBE.lightened(0.08))
	b.box(Vector3(0, 0.15, 0.0285), Vector3(0.022, 0.02, 0.003), DARK)
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.168, 0), 0.056, 0.0, 0.034, 4, Color.WHITE, Color.WHITE, PI * 0.25)
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.blob(Vector3(0, 0.206, 0), Vector3(0.007, 0.007, 0.007), 5, 3, GOLD)
	# corner pennants on the lowest terrace
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(sx * 0.07, 0.05, 0.07), Vector3(sx * 0.07, 0.14, 0.07), 0.0028, M7MeshKit.WOOD)
		b.tag = M7MeshBuilder.TAG_CIV
		b.quad(Vector3(sx * 0.07, 0.136, 0.07), Vector3(sx * 0.07 + sx * 0.026, 0.128, 0.07), Vector3(sx * 0.07 + sx * 0.026, 0.112, 0.07),
			Vector3(sx * 0.07, 0.12, 0.07), Color.WHITE, Vector3(0, 0.12, 0.3))
		b.tag = M7MeshBuilder.TAG_PLAIN
	# a brazier on each side of the stair, glowing
	for sx in [-1.0, 1.0]:
		b.tag = M7MeshBuilder.TAG_PLAIN
		b.prism(Vector3(sx * 0.044, 0.097, 0.05), 0.009, 0.012, 0.012, 6, M7MeshKit.STONE_DARK, M7MeshKit.STONE_DARK)
		b.tag = M7MeshBuilder.TAG_EMISSIVE
		b.prism(Vector3(sx * 0.044, 0.108, 0.05), 0.011, 0.0, 0.02, 6, Color("#ff7a20"), Color("#ff7a20"))
	kit.meshes["decor_temple"] = b.commit()

	# chief's hall (SOC-07): a timber great hall on a raised earth platform under a steep thatch, crossed horns on the gables,
	# carved door posts and a row of shields along the front
	b = kit._b(781)
	b.prism(Vector3.ZERO, 0.13, 0.112, 0.018, 8, M7MeshKit.EARTH, M7MeshKit.EARTH.lightened(0.12))
	var lx := 0.1
	var lz := 0.048
	b.box(Vector3(0, 0.0445, 0), Vector3(lx * 2.0, 0.053, lz * 2.0), M7MeshKit.WOOD_LIGHT, M7MeshKit.WOOD)
	for i in 8:
		var x := -lx + 0.012 + (lx * 2.0 - 0.024) * i / 7.0
		for sz in [-lz, lz]:
			b.pole(Vector3(x, 0.018, sz), Vector3(x, 0.072, sz), 0.0042, M7MeshKit.WOOD)
	var eave := 0.07
	var ridge := 0.158
	var half_w := 0.075
	b.quad(Vector3(-lx - 0.014, eave, half_w), Vector3(lx + 0.014, eave, half_w), Vector3(lx + 0.014, ridge, 0), Vector3(-lx - 0.014, ridge, 0), M7MeshKit.THATCH, Vector3(0, 0.04, 0))
	b.quad(Vector3(-lx - 0.014, eave, -half_w), Vector3(lx + 0.014, eave, -half_w), Vector3(lx + 0.014, ridge, 0), Vector3(-lx - 0.014, ridge, 0), M7MeshKit.THATCH.darkened(0.06), Vector3(0, 0.04, 0))
	for sz in [-1.0, 1.0]:
		for f in [0.34, 0.68]:
			b.pole(Vector3(-lx - 0.012, eave + (ridge - eave) * f, half_w * (1.0 - f) * sz), Vector3(lx + 0.012, eave + (ridge - eave) * f, half_w * (1.0 - f) * sz), 0.0034, M7MeshKit.THATCH_DARK)
	b.tag = M7MeshBuilder.TAG_CIV
	b.pole(Vector3(-lx - 0.012, ridge + 0.002, 0), Vector3(lx + 0.012, ridge + 0.002, 0), 0.0052, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	for sx in [-lx, lx]:
		var d := -1.0 if sx < 0.0 else 1.0
		b.tri(Vector3(sx, eave, lz - 0.003), Vector3(sx, eave, -lz + 0.003), Vector3(sx, ridge - 0.014, 0), M7MeshKit.WOOD_LIGHT, Vector3(0, 0.06, 0))
		b.pole(Vector3(sx + d * 0.016, ridge + 0.04, 0.0), Vector3(sx - d * 0.006, ridge - 0.036, 0.026), 0.0048, M7MeshKit.WOOD)
		b.pole(Vector3(sx + d * 0.016, ridge + 0.04, 0.0), Vector3(sx - d * 0.006, ridge - 0.036, -0.026), 0.0048, M7MeshKit.WOOD)
	b.box(Vector3(0.0, 0.04, lz + 0.001), Vector3(0.034, 0.05, 0.003), DARK)
	b.box(Vector3(0.05, 0.158, 0.0), Vector3(0.024, 0.01, 0.02), DARK)
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(sx * 0.026, 0.018, lz + 0.014), Vector3(sx * 0.026, 0.094, lz + 0.014), 0.0052, M7MeshKit.WOOD)
		b.tag = M7MeshBuilder.TAG_CIV
		b.prism(Vector3(sx * 0.026, 0.06, lz + 0.014), 0.0072, 0.0072, 0.008, 6, Color.WHITE, Color.WHITE)
		b.tag = M7MeshBuilder.TAG_PLAIN
		b.blob(Vector3(sx * 0.026, 0.098, lz + 0.014), Vector3(0.0075, 0.0085, 0.0075), 5, 3, GOLD)
	# shields on the wall: civilization colour and pale, alternating
	for i in 6:
		var x := -0.086 + i * 0.034
		if absf(x) < 0.04:
			continue
		var save := _with(b, Transform3D(Basis(Vector3.RIGHT, PI * 0.5), Vector3(x, 0.05, lz + 0.001)))
		b.tag = M7MeshBuilder.TAG_CIV if i % 2 == 0 else M7MeshBuilder.TAG_PLAIN
		b.prism(Vector3.ZERO, 0.0105, 0.0105, 0.003, 8, Color.WHITE if i % 2 == 0 else SAIL_CREAM, Color.WHITE if i % 2 == 0 else SAIL_CREAM)
		b.xf = save
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.pole(Vector3(-0.096, 0.018, lz + 0.03), Vector3(-0.096, 0.2, lz + 0.03), 0.0034, M7MeshKit.WOOD)
	b.tag = M7MeshBuilder.TAG_CIV
	b.quad(Vector3(-0.094, 0.198, lz + 0.03), Vector3(-0.04, 0.186, lz + 0.03), Vector3(-0.04, 0.152, lz + 0.03), Vector3(-0.094, 0.164, lz + 0.03), Color.WHITE, Vector3(-0.07, 0.17, lz + 0.3))
	kit.meshes["decor_chiefhall"] = b.commit()

	# bales of fleece, a stool and a rack of drying wool
	b = kit._b(782)
	for bale in [[Vector3(-0.026, 0.009, 0.0), 0.0], [Vector3(0.012, 0.009, 0.004), 0.3], [Vector3(-0.007, 0.027, 0.002), 0.15]]:
		var bp: Vector3 = bale[0]
		var sv := _with(b, Transform3D(Basis(Vector3.UP, float(bale[1])), Vector3.ZERO))
		b.box(bp, Vector3(0.032, 0.018, 0.022), FLEECE, FLEECE.lightened(0.05))
		b.box(bp + Vector3(-0.007, 0.0, 0.0), Vector3(0.0022, 0.0185, 0.0225), Color("#8a6a48"))
		b.box(bp + Vector3(0.007, 0.0, 0.0), Vector3(0.0022, 0.0185, 0.0225), Color("#8a6a48"))
		b.xf = sv
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(sx * 0.03 + 0.02, 0, -0.034), Vector3(sx * 0.03 + 0.02, 0.05, -0.034), 0.003, M7MeshKit.WOOD)
	b.pole(Vector3(-0.01, 0.048, -0.034), Vector3(0.05, 0.048, -0.034), 0.0026, M7MeshKit.WOOD)
	for i in 3:
		b.blob(Vector3(0.0 + i * 0.019, 0.034, -0.034), Vector3(0.007, 0.016, 0.006), 5, 3, FLEECE, 0.06)
	kit.meshes["decor_woolbales"] = b.commit()
