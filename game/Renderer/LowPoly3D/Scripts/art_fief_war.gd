## Faith & Fief (E5) defenses: the concentric castle (DEF-09), docs/MVP/32 §4.10.
##
## Not wired to the simulation yet (docs/MVP/32 §9: the FiefFrame comes with the E5 mode plumbing); the meshes are photographed in the Fief gallery
## (LowPoly3DView.show_fief_gallery). Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.1 tall). The castle faces +Z (its
## barbican toward the village); TAG_CIV faces take the owner's colour. It stands on the Citadel hex (docs/MVP/31a), radius about 0.42.
##
##   concentric_1 .. 4    the castle's upgrade: the old castle (art_defense.gd castle_4) stays standing inside while a second, lower ring of round towers and
##                        curtain wall rises round it, with a killing ground between the walls and a barbican on the front: foundations, walls rising on
##                        scaffolding, walls and towers up, finished with conical roofs and banners
##   concentric_4_idle    the finished castle with nobody in it: the banners lowered and no pennants, because the defense bonus applies only while garrisoned
##   cannon               WAR-12: an iron barrel with a swelling muzzle on a timber carriage with two spoked wheels and a trail, a rammer, a pile of shot and
##                        a powder keg beside it; faces +Z (the rig adds the flash, smoke and recoil)
##   wall_seg_cracked     docs/MVP/32 §8.3 (integrity 40 to 70%): a wall run, cracked through on both faces, stones fallen at its foot
##   wall_seg_breach      (15 to 40%): a gap through the curtain with a rubble slope and broken, stepped ends
##   wall_tower_cracked   a tower split by a crack on its faces
##   wall_tower_breach    a tower half fallen: two walls still standing at the back corner, the others broken down to rubble
##   wall_gate_broken     a gate with its doors splintered, one hanging from its hinge, planks fallen and a scorch over the arch
##                        (wall_ruin and wall_rubble, art_war.gd, stay the last stages)
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const SLIT := Color("#2a2119")
const TIMBER := Color("#6b5236")
const TIMBER_LIGHT := Color("#8a6a48")
const ROOF := Color("#a8492c")
const ROOF_DARK := Color("#7f3622")
const EARTH := Color("#7b6247")
const GROUND := Color("#8a7254")           # the trodden killing ground between the two rings
const IRON := Color("#3f444c")
const IRON_LIGHT := Color("#6b717a")
const CRACK := Color("#2a2119")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV

const SEG := 0.16           # the same wall pieces as art_defense.gd
const WALL_H := 0.075
const WALL_T := 0.036

const OUT := 0.36                           # half-side of the outer ring (centres of the corner towers)
const INNER_SCALE := 0.9                    # the old castle, drawn a little smaller so the killing ground fits between the rings
const OUTER_H := 0.066
const OUTER_T := 0.032

static func build(kit) -> void:
	for stage in range(1, 5):
		kit.meshes["concentric_%d" % stage] = _castle(kit, stage, false)
	kit.meshes["concentric_4_idle"] = _castle(kit, 4, true)
	kit.meshes["cannon"] = _cannon(kit)
	kit.meshes["wall_seg_cracked"] = _seg_cracked(kit)
	kit.meshes["wall_seg_breach"] = _seg_breach(kit)
	kit.meshes["wall_tower_cracked"] = _tower_cracked(kit)
	kit.meshes["wall_tower_breach"] = _tower_breach(kit)
	kit.meshes["wall_gate_broken"] = _gate_broken(kit)

# ------------------------------------------------------------------ pieces

## Copies the triangles of a finished mesh into the builder (the old castle stays standing inside the new ring). With `skip_civ`, the owner-coloured
## cloth is left out so the caller can hang its own banner.
static func _append(b: M7MeshBuilder, mesh: ArrayMesh, xf: Transform3D, skip_civ: bool) -> void:
	if mesh == null or mesh.get_surface_count() == 0:
		return
	var arr := mesh.surface_get_arrays(0)
	var vs: PackedVector3Array = arr[Mesh.ARRAY_VERTEX]
	var ns: PackedVector3Array = arr[Mesh.ARRAY_NORMAL]
	var cs: PackedColorArray = arr[Mesh.ARRAY_COLOR]
	for i in range(0, vs.size(), 3):
		if skip_civ and cs[i].a < 0.01:
			continue
		for k in 3:
			b.v.append(xf * vs[i + k])
			b.n.append((xf.basis * ns[i + k]).normalized())
			b.c.append(cs[i + k])

## Merlons round the rim of a round tower or on a straight run, `n` of them on a circle of radius `r` at height `y` above `p`.
static func _round_merlons(b: M7MeshBuilder, p: Vector3, r: float, y: float, n: int) -> void:
	for k in n:
		var a := TAU * k / n
		b.box(p + Vector3(cos(a) * r, y + 0.008, sin(a) * r), Vector3(0.017, 0.016, 0.017), STONE_LIGHT)

## A round tower on `p` (foot centre): a battered foot, a shaft with a string course and slits, then a crenellated top or a conical slate-red roof.
static func _round_tower(b: M7MeshBuilder, p: Vector3, r: float, h: float, roofed: bool, face: float = 0.0) -> void:
	b.prism(p + Vector3(0, -0.03, 0), r * 1.32, r * 1.1, 0.03 + h * 0.16, 8, STONE_DARK, STONE_DARK, PI / 8.0)
	b.prism(p + Vector3(0, h * 0.16, 0), r * 1.1, r * 1.0, h * 0.84, 8, STONE, STONE_LIGHT, PI / 8.0)
	if h > 0.06:
		b.prism(p + Vector3(0, h * 0.6, 0), r * 1.07, r * 1.07, 0.007, 8, STONE_LIGHT, STONE_LIGHT, PI / 8.0)
		for k in 3:
			var a := face + (k - 1) * 0.9
			b.box(p + Vector3(cos(a) * r * 1.0, h * 0.42, sin(a) * r * 1.0), Vector3(0.007, 0.024, 0.007), SLIT)
	if roofed:
		b.prism(p + Vector3(0, h, 0), r * 1.2, r * 1.2, 0.008, 8, ROOF_DARK, ROOF_DARK, PI / 8.0)
		b.prism(p + Vector3(0, h + 0.006, 0), r * 1.18, 0.0, r * 2.1, 8, ROOF, ROOF_DARK, PI / 8.0)
	else:
		b.prism(p + Vector3(0, h, 0), r * 1.12, r * 1.12, 0.01, 8, STONE_LIGHT, STONE_LIGHT, PI / 8.0)
		_round_merlons(b, p, r * 0.95, h + 0.008, 8)

## A straight curtain between two points at the same height (axis-aligned), `h` high; `h` of zero draws nothing.
static func _run(b: M7MeshBuilder, a: Vector3, c: Vector3, h: float, crenel: bool) -> void:
	if h <= 0.0:
		return
	var d := c - a
	var mid := (a + c) * 0.5
	var horizontal := absf(d.x) >= absf(d.z)
	var length := absf(d.x) if horizontal else absf(d.z)
	var size := Vector3(length, h + 0.03, OUTER_T) if horizontal else Vector3(OUTER_T, h + 0.03, length)
	b.box(mid + Vector3(0, (h - 0.03) * 0.5, 0), size, STONE, STONE_LIGHT)
	if crenel:
		var n := maxi(2, int(round(length / 0.034)))
		var across := Vector3(0, 0, 1) if horizontal else Vector3(1, 0, 0)
		var along := Vector3(1, 0, 0) if horizontal else Vector3(0, 0, 1)
		for i in n:
			if i % 2 == 1:
				continue
			var s := -length * 0.5 + (i + 0.5) * (length / n)
			for side in [-1.0, 1.0]:
				var q: Vector3 = mid + Vector3(0, h + 0.007, 0) + along * s + across * side * (OUTER_T * 0.5 - 0.004)
				b.box(q, Vector3(0.022 if horizontal else 0.008, 0.014, 0.008 if horizontal else 0.022), STONE_LIGHT)

## A banner on a pole: raised (flying at the top) or lowered (furled and hanging at half-mast).
static func _banner(b: M7MeshBuilder, base: Vector3, height: float, lowered: bool) -> void:
	b.pole(base, base + Vector3(0, height, 0), 0.0025, TIMBER)
	b.tag = CIV
	if lowered:
		var top := base + Vector3(0, height * 0.58, 0)
		b.quad(top, top + Vector3(0.012, 0, 0), top + Vector3(0.008, -0.05, 0), top + Vector3(0.0, -0.05, 0), Color.WHITE, top + Vector3(0, 0, 1.0))
	else:
		var top := base + Vector3(0, height, 0)
		b.quad(top, top + Vector3(0.075, -0.012, 0), top + Vector3(0.075, -0.044, 0), top + Vector3(0, -0.034, 0), Color.WHITE, top + Vector3(0, 0, 1.0))
	b.tag = PLAIN

# ------------------------------------------------------------------ the castle

## stage 1 foundations, 2 walls rising with scaffolding, 3 walls and towers up, 4 finished with roofs and banners; `idle` lowers the banners.
static func _castle(kit, stage: int, idle: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1120 + stage + (9 if idle else 0))
	b.tag = PLAIN
	# the killing ground: trodden earth to the foot of the outer wall, and the old castle standing in the middle of it
	b.box(Vector3(0, 0.004, 0), Vector3(OUT * 2.0 + 0.16, 0.012, OUT * 2.0 + 0.16), GROUND)
	_append(b, kit.meshes["castle_4"], Transform3D(Basis.from_scale(Vector3.ONE * INNER_SCALE), Vector3.ZERO), true)
	var wh: float = [0.0, 0.014, 0.04, OUTER_H, OUTER_H][stage]
	var th: float = [0.0, 0.016, 0.06, 0.112, 0.118][stage]
	var done := stage == 4
	var gap := 0.075
	# the outer curtain, with the barbican gap in the front (+Z) side
	_run(b, Vector3(-OUT, 0, -OUT), Vector3(OUT, 0, -OUT), wh, stage >= 3)
	_run(b, Vector3(-OUT, 0, -OUT), Vector3(-OUT, 0, OUT), wh, stage >= 3)
	_run(b, Vector3(OUT, 0, -OUT), Vector3(OUT, 0, OUT), wh, stage >= 3)
	_run(b, Vector3(-OUT, 0, OUT), Vector3(-gap, 0, OUT), wh, stage >= 3)
	_run(b, Vector3(gap, 0, OUT), Vector3(OUT, 0, OUT), wh, stage >= 3)
	# six round towers: the four corners and the middle of the back and side walls
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			_round_tower(b, Vector3(sx * OUT, 0, sz * OUT), 0.054, th, done, atan2(sz, sx))
	for p in [Vector3(0, 0, -OUT), Vector3(-OUT, 0, 0), Vector3(OUT, 0, 0)]:
		_round_tower(b, p, 0.04, th * 0.9, done, atan2(p.z, p.x))
	# the barbican: two small round towers either side of the gap and a short walled passage out to a timber gate, with a drawbridge plank laid down
	if stage >= 3:
		for sx in [-1.0, 1.0]:
			_round_tower(b, Vector3(sx * (gap + 0.006), 0, OUT), 0.032, th * 0.95, done, PI * 0.5)
			_run(b, Vector3(sx * (gap - 0.012), 0, OUT), Vector3(sx * (gap - 0.012), 0, OUT + 0.085), OUTER_H * 0.8, true)
		b.box(Vector3(0, 0.032, OUT + 0.085), Vector3(gap * 2.0 - 0.018, 0.06, 0.006), TIMBER)
		b.box(Vector3(0, 0.07, OUT + 0.085), Vector3(gap * 2.0 + 0.012, 0.012, 0.012), STONE)
		b.box(Vector3(0, 0.004, OUT + 0.15), Vector3(0.07, 0.006, 0.12), TIMBER_LIGHT)
	elif stage == 2:
		for sx in [-1.0, 1.0]:
			b.prism(Vector3(sx * (gap + 0.006), -0.02, OUT), 0.04, 0.034, 0.045, 8, STONE_DARK, STONE_DARK, PI / 8.0)
	# the owner's banners (a pennant on each front gate tower and the old keep's banner, rehung): flying when the castle is garrisoned
	if done:
		# the old keep stands at (0, 0, -0.21 * 0.28) and its roof apex is 0.305 up, both scaled with the castle (art_defense.gd `_castle`)
		_banner(b, Vector3(0, 0.27 * INNER_SCALE, -0.21 * 0.28 * INNER_SCALE), 0.115, idle)
		if not idle:
			for sx in [-1.0, 1.0]:
				_banner(b, Vector3(sx * (gap + 0.006), th * 0.95 + 0.06, OUT), 0.07, false)
	# scaffolding on the new ring while it rises, stone and timber stacked in the killing ground
	if stage in [2, 3]:
		for sx in [-0.26, -0.13, 0.0, 0.13, 0.26]:
			b.pole(Vector3(sx, -0.01, OUT + 0.036), Vector3(sx, 0.05 + 0.03 * (stage - 2), OUT + 0.036), 0.0032, TIMBER_LIGHT)
		for y in ([0.03] if stage == 2 else [0.03, 0.06]):
			b.box(Vector3(0, y, OUT + 0.036), Vector3(0.58, 0.005, 0.03), TIMBER)
	if stage < 4:
		for k in 5:
			b.blob(Vector3(-0.22 + 0.034 * k, 0.018, OUT - 0.1), Vector3(0.02, 0.014, 0.017), 5, 3, STONE_LIGHT, 0.12, STONE)
		b.box(Vector3(0.22, 0.018, OUT - 0.1), Vector3(0.08, 0.016, 0.03), TIMBER_LIGHT)
		# a treadwheel crane over the work in stages 2 and 3: a mast and a jib with a stone on its hook
		if stage >= 2:
			b.pole(Vector3(-0.2, 0.0, -OUT + 0.1), Vector3(-0.2, 0.14, -OUT + 0.1), 0.004, TIMBER)
			b.pole(Vector3(-0.2, 0.12, -OUT + 0.1), Vector3(-0.12, 0.15, -OUT + 0.1), 0.003, TIMBER_LIGHT)
			b.pole(Vector3(-0.13, 0.148, -OUT + 0.1), Vector3(-0.13, 0.1, -OUT + 0.1), 0.0012, Color("#b9a68a"))
			b.box(Vector3(-0.13, 0.092, -OUT + 0.1), Vector3(0.016, 0.014, 0.016), STONE_LIGHT)
	return b.commit()

# ------------------------------------------------------------------ cannon (WAR-12)

static func _wheel(b: M7MeshBuilder, p: Vector3, r: float) -> void:
	var save := b.xf
	# the wheel's axle is along X: spin the local frame so its +Y axis points to -X and the wheel lies in the local XZ plane
	b.xf = b.xf * Transform3D(Basis(Vector3.BACK, PI * 0.5), p)
	b.prism(Vector3(0, -0.003, 0), r, r, 0.006, 10, TIMBER, TIMBER_LIGHT)
	for k in 5:
		var a := PI * k / 5.0
		b.pole(Vector3(cos(a) * r * 0.9, 0.0, sin(a) * r * 0.9), Vector3(-cos(a) * r * 0.9, 0.0, -sin(a) * r * 0.9), 0.0024, TIMBER)
	b.prism(Vector3(0, -0.005, 0), r * 0.2, r * 0.2, 0.01, 6, IRON, IRON)
	b.xf = save

static func _cannon(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1160)
	b.tag = PLAIN
	var wr := 0.022
	# the carriage: two cheeks along Z stepped down to the trail, an axle, a transom, and two wheels
	for sx in [-0.0125, 0.0125]:
		b.box(Vector3(sx, 0.03, 0.01), Vector3(0.008, 0.016, 0.07), TIMBER)
		b.box(Vector3(sx, 0.026, -0.05), Vector3(0.008, 0.01, 0.05), TIMBER_LIGHT)
		b.box(Vector3(sx, 0.014, -0.085), Vector3(0.008, 0.008, 0.024), TIMBER)
	b.pole(Vector3(-0.034, wr, 0.0), Vector3(0.034, wr, 0.0), 0.004, IRON)
	b.box(Vector3(0, 0.026, -0.03), Vector3(0.03, 0.008, 0.006), TIMBER)
	for sx in [-0.034, 0.034]:
		_wheel(b, Vector3(sx, wr, 0.0), wr)
	# the barrel: an iron tube along Z, thicker at the breech, a cascabel knob, a ring before the muzzle and a swelling muzzle; trunnions through the cheeks
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis(Vector3.RIGHT, PI * 0.5), Vector3(0, 0.046, -0.055))
	b.prism(Vector3(0, 0.0, 0), 0.0135, 0.0105, 0.12, 8, IRON, IRON_LIGHT)
	b.prism(Vector3(0, 0.0, 0), 0.0145, 0.0145, 0.008, 8, IRON_LIGHT, IRON_LIGHT)
	b.prism(Vector3(0, 0.07, 0), 0.0115, 0.0115, 0.006, 8, IRON_LIGHT, IRON_LIGHT)
	b.prism(Vector3(0, 0.108, 0), 0.0125, 0.0135, 0.014, 8, IRON, IRON_LIGHT)
	b.prism(Vector3(0, 0.12, 0), 0.0075, 0.0075, 0.002, 8, SLIT, SLIT)
	b.blob(Vector3(0, -0.006, 0), Vector3(0.007, 0.007, 0.007), 5, 3, IRON, 0.0)
	b.xf = save
	b.pole(Vector3(-0.0175, 0.046, -0.01), Vector3(0.0175, 0.046, -0.01), 0.0035, IRON)
	b.box(Vector3(0, 0.0605, -0.05), Vector3(0.003, 0.004, 0.003), SLIT)
	# the rammer lying along the carriage, a pyramid of shot, a powder keg
	b.pole(Vector3(0.05, 0.004, -0.06), Vector3(0.05, 0.004, 0.08), 0.0028, TIMBER_LIGHT)
	b.prism(Vector3(0.05, 0.004, 0.08), 0.006, 0.006, 0.01, 6, TIMBER, TIMBER)
	for k in 6:
		var row := 0 if k < 3 else (1 if k < 5 else 2)
		var idx := k if k < 3 else (k - 3 if k < 5 else 0)
		var span := 3 - row
		b.blob(Vector3(-0.07 + 0.012 * (idx - (span - 1) * 0.5) + 0.036, 0.007 + 0.011 * row, 0.06 + 0.0 * row), Vector3(0.0075, 0.0075, 0.0075), 5, 3, IRON_LIGHT, 0.0, IRON)
	b.prism(Vector3(-0.065, 0.0, -0.04), 0.011, 0.011, 0.02, 8, Color("#4a3a2a"), Color("#5e4a36"))
	b.prism(Vector3(-0.065, 0.005, -0.04), 0.0118, 0.0118, 0.002, 8, IRON, IRON)
	return b.commit()

# ------------------------------------------------------------------ what a siege leaves of the walls (docs/MVP/32 §8.3)

## A crack as a zig-zag of thin dark poles through the points, on the plane z.
static func _crack(b: M7MeshBuilder, pts: Array, z: float) -> void:
	for k in pts.size() - 1:
		var a: Vector2 = pts[k]
		var c: Vector2 = pts[k + 1]
		b.pole(Vector3(a.x, a.y, z), Vector3(c.x, c.y, z), 0.0016, CRACK)

static func _seg_cracked(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1161)
	b.tag = PLAIN
	_append(b, kit.meshes["wall_seg"], Transform3D.IDENTITY, false)
	var zf := WALL_T * 0.5 + 0.0012
	for side in [-1.0, 1.0]:
		var z: float = side * zf
		_crack(b, [Vector2(0.012, 0.078), Vector2(0.004, 0.06), Vector2(0.016, 0.046), Vector2(0.002, 0.03), Vector2(0.012, 0.012)], z)
		_crack(b, [Vector2(0.004, 0.06), Vector2(-0.014, 0.052), Vector2(-0.02, 0.036)], z)
		# a chipped place on the crown where merlons have fallen
		b.box(Vector3(0.018 * side, 0.074, z), Vector3(0.03, 0.01, 0.0015), STONE_DARK)
	# fallen stones at the foot on both sides
	for k in 5:
		var sx := -0.05 + 0.03 * k
		var sd: float = 1.0 if k % 2 == 0 else -1.0
		b.blob(Vector3(sx, 0.007, sd * (WALL_T * 0.5 + 0.014)), Vector3(0.011, 0.008, 0.009), 5, 2, STONE_LIGHT if k % 2 == 0 else STONE, 0.2, STONE_DARK)
	return b.commit()

static func _seg_breach(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1162)
	b.tag = PLAIN
	b.box(Vector3(0, 0.006, 0), Vector3(SEG, 0.018, 0.044), STONE_DARK)
	# the two ends stand nearly to height and step down toward the gap, the stone pale where it broke
	for side in [-1.0, 1.0]:
		b.box(Vector3(side * 0.066, 0.04, 0), Vector3(0.028, 0.07, WALL_T), STONE, STONE_LIGHT)
		b.box(Vector3(side * 0.045, 0.034, 0), Vector3(0.016, 0.052, WALL_T), STONE, STONE_LIGHT)
		b.box(Vector3(side * 0.033, 0.028, 0), Vector3(0.012, 0.036, WALL_T), STONE, STONE_LIGHT)
		b.box(Vector3(side * 0.0245, 0.022, 0), Vector3(0.005, 0.022, WALL_T - 0.004), STONE_LIGHT)
		b.box(Vector3(side * 0.057, 0.079, 0), Vector3(0.012, 0.008, 0.02), STONE_LIGHT)
		# a few merlons left on the standing top
		b.box(Vector3(side * 0.074, 0.083, WALL_T * 0.5 - 0.004), Vector3(0.02, 0.014, 0.008), STONE_LIGHT)
	# the rubble slope in the gap, rising from both sides, with a few big fallen blocks
	for k in 7:
		var x := -0.032 + 0.0107 * k
		var h := 0.012 + 0.008 * (1.0 - absf(x) / 0.032)
		b.blob(Vector3(x, 0.012 + h * 0.4, 0.004 * sin(k * 2.0)), Vector3(0.014, h * 0.9, 0.022), 5, 2, STONE_LIGHT if k % 2 == 0 else STONE, 0.22, STONE_DARK)
	for k in 3:
		b.box(Vector3(-0.012 + 0.014 * k, 0.012, 0.03 + 0.006 * (k % 2)), Vector3(0.016, 0.012, 0.012), STONE_LIGHT)
	b.blob(Vector3(0.0, 0.01, -0.03), Vector3(0.026, 0.008, 0.012), 5, 2, STONE, 0.2, STONE_DARK)
	return b.commit()

static func _tower_cracked(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1163)
	b.tag = PLAIN
	_append(b, kit.meshes["wall_tower"], Transform3D.IDENTITY, false)
	# a long crack down two faces (the +Z face and the +X face), and fallen stones round the foot
	_crack(b, [Vector2(0.008, 0.124), Vector2(-0.004, 0.1), Vector2(0.01, 0.08), Vector2(-0.006, 0.058), Vector2(0.006, 0.03)], 0.0318)
	_crack(b, [Vector2(0.002, 0.1), Vector2(0.02, 0.092)], 0.0318)
	for k in 5:
		var a := TAU * k / 5.0 + 0.4
		b.blob(Vector3(cos(a) * 0.05, 0.007, sin(a) * 0.05), Vector3(0.011, 0.008, 0.01), 5, 2, STONE_LIGHT if k % 2 == 0 else STONE, 0.2, STONE_DARK)
	return b.commit()

static func _tower_breach(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1164)
	b.tag = PLAIN
	var r := 0.031
	var t := 0.011
	# the foot is whole
	b.prism(Vector3(0, -0.03, 0), 0.058, 0.045, 0.048, 4, STONE_DARK, STONE_DARK, PI / 4.0)
	# the two back walls still stand to the top (with merlons), the two front ones are broken down in steps
	b.box(Vector3(0, 0.07, -r), Vector3(r * 2.0 + t, 0.1, t), STONE, STONE_LIGHT)
	b.box(Vector3(-r, 0.07, 0), Vector3(t, 0.1, r * 2.0), STONE, STONE_LIGHT)
	for k in 3:
		b.box(Vector3(-r + 0.02 * k, 0.126, -r), Vector3(0.012, 0.014, 0.01), STONE_LIGHT)
	b.box(Vector3(-r, 0.126, 0.012), Vector3(0.01, 0.014, 0.012), STONE_LIGHT)
	b.box(Vector3(0.0, 0.056, -r - 0.0055), Vector3(0.008, 0.026, 0.002), SLIT)
	b.box(Vector3(-r - 0.0055, 0.056, 0.0), Vector3(0.002, 0.026, 0.008), SLIT)
	# the front (+Z) wall: broken down to a ragged half, the right (+X) wall to a low stump
	b.box(Vector3(-0.02, 0.05, r), Vector3(0.026, 0.06, t), STONE, STONE_LIGHT)
	b.box(Vector3(0.0, 0.034, r), Vector3(0.02, 0.028, t), STONE, STONE_LIGHT)
	b.box(Vector3(0.018, 0.02, r), Vector3(0.016, 0.01, t), STONE, STONE_LIGHT)
	b.box(Vector3(r, 0.03, -0.014), Vector3(t, 0.036, 0.03), STONE, STONE_LIGHT)
	b.box(Vector3(r, 0.018, 0.012), Vector3(t, 0.01, 0.02), STONE, STONE_LIGHT)
	# the floor inside, and the rubble spilled out over the breach and the ground
	b.box(Vector3(0.0, 0.026, 0.0), Vector3(r * 1.8, 0.004, r * 1.8), STONE_DARK)
	for k in 7:
		var a := -0.2 + 0.5 * k
		b.blob(Vector3(0.03 + 0.012 * cos(a * 2.0) + 0.01 * (k % 3), 0.012 + 0.006 * (k % 3), 0.04 + 0.03 * sin(a)), Vector3(0.013, 0.011, 0.012), 5, 2,
			STONE_LIGHT if k % 2 == 0 else STONE, 0.22, STONE_DARK)
	return b.commit()

static func _gate_broken(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1165)
	b.tag = PLAIN
	_append(b, kit.meshes["wall_gate"], Transform3D.IDENTITY, false)
	# the opening is 0.072 wide between the posts; a scorch over the arch, the doors smashed: one leaf still on its hinge, hanging askew
	b.box(Vector3(0, 0.08, WALL_T * 0.5 + 0.001), Vector3(0.1, 0.02, 0.0015), Color("#3a3128"))
	b.box(Vector3(0, 0.07, -WALL_T * 0.5 - 0.001), Vector3(0.1, 0.02, 0.0015), Color("#3a3128"))
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis(Vector3.UP, 0.7) * Basis(Vector3.BACK, 0.18), Vector3(-0.034, 0.03, 0.026))
	b.box(Vector3(0.0, 0.0, 0.0), Vector3(0.004, 0.056, 0.03), TIMBER_LIGHT)
	b.box(Vector3(0.0035, 0.012, 0.0), Vector3(0.003, 0.004, 0.03), IRON)
	b.box(Vector3(0.0035, -0.012, 0.0), Vector3(0.003, 0.004, 0.03), IRON)
	b.xf = save
	# planks and splinters fallen on the ground in front, the other leaf in pieces
	b.xf = b.xf * Transform3D(Basis(Vector3.UP, -0.5), Vector3(0.02, 0.004, 0.04))
	b.box(Vector3(0, 0, 0), Vector3(0.036, 0.004, 0.008), TIMBER)
	b.box(Vector3(0.006, 0.004, 0.01), Vector3(0.03, 0.004, 0.008), TIMBER_LIGHT)
	b.xf = save
	b.xf = b.xf * Transform3D(Basis(Vector3.UP, 0.9), Vector3(0.036, 0.004, 0.062))
	b.box(Vector3(0, 0, 0), Vector3(0.026, 0.004, 0.008), TIMBER)
	b.xf = save
	for k in 4:
		b.blob(Vector3(-0.04 + 0.026 * k, 0.007, 0.046 + 0.01 * (k % 2)), Vector3(0.011, 0.008, 0.009), 5, 2, STONE_LIGHT if k % 2 == 0 else STONE, 0.2, STONE_DARK)
	return b.commit()
