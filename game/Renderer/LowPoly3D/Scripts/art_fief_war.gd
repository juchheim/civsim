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
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV

const OUT := 0.36                           # half-side of the outer ring (centres of the corner towers)
const INNER_SCALE := 0.9                    # the old castle, drawn a little smaller so the killing ground fits between the rings
const OUTER_H := 0.066
const OUTER_T := 0.032

static func build(kit) -> void:
	for stage in range(1, 5):
		kit.meshes["concentric_%d" % stage] = _castle(kit, stage, false)
	kit.meshes["concentric_4_idle"] = _castle(kit, 4, true)

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
