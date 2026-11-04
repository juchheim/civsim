## Iron Age defenses: the stone wall ring (DEF-05 stone walls, DEF-06 towers and gatehouses) and the castle (DEF-08), with the stages of
## its building.
##
## Meshes are laid out by world_builder.gd (see `_stone_walls` and `_castle_site`). Same units as the rest of the kit (1.0 = one tile
## column, an adult is ~0.1 tall) and the same tags (TAG_CIV faces take the owner's colour). Wall pieces run along local X with their
## front (+Z) toward the village; the castle and gatehouses face +Z toward the village too.
##
##   wall_seg          one run of the ring, a stone curtain with a crenellated top, 0.16 long
##   wall_tower        a square tower on the ring, taller than the wall
##   wall_gate         a plain gate in the ring: two stone posts, a timber lintel and open doors
##   wall_gatehouse    DEF-06: a gate between two roofed towers with a bridge room over the passage
##   castle_1 .. 4     DEF-08: foundations, walls rising, walls and towers up with the keep scaffolded, finished with banners
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const SILL := Color("#7a766d")
const SLIT := Color("#2a2119")
const TIMBER := Color("#6b5236")
const TIMBER_LIGHT := Color("#8a6a48")
const ROOF := Color("#a8492c")
const ROOF_DARK := Color("#7f3622")
const EARTH := Color("#7b6247")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV

const SEG := 0.16          # length of one wall run
const WALL_H := 0.075
const WALL_T := 0.036
const TOWER_H := 0.125

static func build(kit) -> void:
	kit.meshes["wall_seg"] = _seg(kit)
	kit.meshes["wall_tower"] = _tower(kit)
	kit.meshes["wall_gate"] = _gate(kit)
	kit.meshes["wall_gatehouse"] = _gatehouse(kit)
	for stage in range(1, 5):
		kit.meshes["castle_%d" % stage] = _castle(kit, stage)

# ------------------------------------------------------------------ shared pieces

## Crenellations along a wall top: merlons on both faces, `along` is the unit direction of the run, `half` half its length.
static func _merlons(b: M7MeshBuilder, centre: Vector3, along: Vector3, half: float, thick: float, col: Color) -> void:
	var across := Vector3(-along.z, 0.0, along.x)
	var n := maxi(2, int(round(half * 2.0 / 0.036)))
	for i in n:
		if i % 2 == 1:
			continue
		var s := -half + (i + 0.5) * (half * 2.0 / n)
		for side in [-1.0, 1.0]:
			var p: Vector3 = centre + along * s + across * side * (thick * 0.5 - 0.004)
			b.box(p + Vector3(0, 0.007, 0), Vector3(0.026 if absf(along.x) > 0.5 else 0.008, 0.014, 0.026 if absf(along.z) > 0.5 else 0.008), col)

## A straight curtain from a to b (axis-aligned, same y), `h` high and `t` thick, base buried below the floor, with merlons.
static func _curtain(b: M7MeshBuilder, a: Vector3, c: Vector3, h: float, t: float, col: Color, top: Color, crenel: bool = true) -> void:
	var d := c - a
	var mid := (a + c) * 0.5
	var horizontal := absf(d.x) >= absf(d.z)
	var length := absf(d.x) if horizontal else absf(d.z)
	var size := Vector3(length, h + 0.03, t) if horizontal else Vector3(t, h + 0.03, length)
	b.box(mid + Vector3(0, (h - 0.03) * 0.5, 0), size, col, top)
	if crenel and h > 0.04:
		_merlons(b, mid + Vector3(0, h, 0), Vector3(1, 0, 0) if horizontal else Vector3(0, 0, 1), length * 0.5, t, STONE_LIGHT)

## A square tower standing on `p` (foot centre), half-width `r`, `h` high; optionally roofed with a pyramid, otherwise crenellated.
static func _tower_at(b: M7MeshBuilder, p: Vector3, r: float, h: float, roofed: bool, slit_side: float = -1.0) -> void:
	# battered foot, shaft, a stone string course, then the top
	b.prism(p + Vector3(0, -0.03, 0), r * 1.45, r * 1.12, 0.03 + h * 0.18, 4, STONE_DARK, STONE_DARK, PI / 4.0)
	b.prism(p + Vector3(0, h * 0.18, 0), r * 1.12, r * 1.02, h * 0.82, 4, STONE, STONE_LIGHT, PI / 4.0)
	b.prism(p + Vector3(0, h * 0.62, 0), r * 1.12, r * 1.12, 0.008, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)
	# arrow slits on the outer (-Z) and side faces
	var sz := p.z + slit_side * r * 1.03
	b.box(Vector3(p.x, p.y + h * 0.5, sz), Vector3(0.008, 0.026, 0.004), SLIT)
	b.box(Vector3(p.x - r * 1.0, p.y + h * 0.5, p.z), Vector3(0.004, 0.026, 0.008), SLIT)
	b.box(Vector3(p.x + r * 1.0, p.y + h * 0.5, p.z), Vector3(0.004, 0.026, 0.008), SLIT)
	if roofed:
		b.prism(p + Vector3(0, h, 0), r * 1.35, 0.0, r * 1.5, 4, ROOF, ROOF, PI / 4.0)
		b.prism(p + Vector3(0, h - 0.006, 0), r * 1.4, r * 1.4, 0.008, 4, ROOF_DARK, ROOF_DARK, PI / 4.0)
	else:
		b.prism(p + Vector3(0, h, 0), r * 1.18, r * 1.18, 0.012, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)
		for sx in [-1.0, 1.0]:
			for sz2 in [-1.0, 1.0]:
				b.box(p + Vector3(sx * r * 0.82, h + 0.02, sz2 * r * 0.82), Vector3(0.022, 0.016, 0.022), STONE_LIGHT)
		for k in 4:
			var a := k * PI * 0.5
			b.box(p + Vector3(cos(a) * r * 0.9, h + 0.02, sin(a) * r * 0.9), Vector3(0.018, 0.014, 0.018), STONE_LIGHT)

# ------------------------------------------------------------------ the ring

static func _seg(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(901)
	b.tag = PLAIN
	_curtain(b, Vector3(-SEG * 0.5, 0, 0), Vector3(SEG * 0.5, 0, 0), WALL_H, WALL_T, STONE, STONE_LIGHT)
	# a plinth course and a few darker blocks so the wall does not read as a plain slab
	b.box(Vector3(0, 0.006, 0), Vector3(SEG, 0.018, WALL_T + 0.008), STONE_DARK)
	for i in 3:
		b.box(Vector3(-0.05 + 0.05 * i, 0.04, WALL_T * 0.5 + 0.001), Vector3(0.03, 0.008, 0.002), SILL)
	return b.commit()

static func _tower(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(902)
	b.tag = PLAIN
	_tower_at(b, Vector3.ZERO, 0.04, TOWER_H, false)
	return b.commit()

static func _gate(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(903)
	b.tag = PLAIN
	var open := 0.072
	for side in [-1.0, 1.0]:
		var x0: float = side * open * 0.5
		var x1: float = side * SEG * 0.5
		_curtain(b, Vector3(x0, 0, 0), Vector3(x1, 0, 0), WALL_H, WALL_T, STONE, STONE_LIGHT)
		b.box(Vector3(side * (open * 0.5 + 0.007), 0.04, 0), Vector3(0.016, 0.082, WALL_T + 0.008), STONE_LIGHT)
	# timber lintel over the opening and a short wall above it, doors standing open
	b.box(Vector3(0, 0.062, 0), Vector3(open + 0.03, 0.014, WALL_T), TIMBER)
	b.box(Vector3(0, 0.074, 0), Vector3(open + 0.03, 0.012, WALL_T), STONE)
	_merlons(b, Vector3(0, 0.08, 0), Vector3(1, 0, 0), (open + 0.03) * 0.5, WALL_T, STONE_LIGHT)
	for side in [-1.0, 1.0]:
		b.box(Vector3(side * (open * 0.5 - 0.004), 0.028, 0.02), Vector3(0.004, 0.056, 0.034), TIMBER_LIGHT)
	return b.commit()

static func _gatehouse(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(904)
	b.tag = PLAIN
	var tx := 0.105
	for side in [-1.0, 1.0]:
		_tower_at(b, Vector3(side * tx, 0, 0), 0.047, 0.15, true)
	# the bridge room over the passage and the passage itself: a dark arch with a raised portcullis grille and open doors
	b.box(Vector3(0, 0.112, 0), Vector3(0.12, 0.05, 0.056), STONE, STONE_LIGHT)
	_merlons(b, Vector3(0, 0.137, 0), Vector3(1, 0, 0), 0.06, 0.056, STONE_LIGHT)
	b.box(Vector3(0, 0.043, 0.0), Vector3(0.078, 0.086, 0.062), STONE_DARK)
	b.box(Vector3(0, 0.043, 0.032), Vector3(0.056, 0.082, 0.004), SLIT)
	b.box(Vector3(0, 0.043, -0.032), Vector3(0.056, 0.082, 0.004), SLIT)
	for gx in [-0.02, -0.007, 0.007, 0.02]:
		b.box(Vector3(gx, 0.078, -0.034), Vector3(0.003, 0.02, 0.003), TIMBER)
	b.box(Vector3(0, 0.088, -0.034), Vector3(0.056, 0.003, 0.003), TIMBER)
	# the owner's pennant on the left tower
	b.tag = CIV
	var top := Vector3(-tx, 0.15 + 0.047 * 1.5 + 0.01, 0)
	b.pole(top, top + Vector3(0, 0.06, 0), 0.0022, TIMBER)
	b.quad(top + Vector3(0, 0.056, 0), top + Vector3(0.044, 0.046, 0), top + Vector3(0.044, 0.026, 0), top + Vector3(0, 0.032, 0), Color.WHITE,
		top + Vector3(0, 0.04, 1.0))
	b.tag = PLAIN
	return b.commit()

# ------------------------------------------------------------------ the castle

## stage 1 foundations, 2 walls rising with scaffolding, 3 walls and towers up and the keep still scaffolded, 4 finished.
static func _castle(kit, stage: int) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(910 + stage)
	b.tag = PLAIN
	var half := 0.21
	var wh: float = [0.0, 0.014, 0.04, WALL_H + 0.01, WALL_H + 0.01][stage]
	var th: float = [0.0, 0.014, 0.065, 0.14, 0.15][stage]
	var kh: float = [0.0, 0.014, 0.1, 0.17, 0.2][stage]
	# bailey ground: trodden earth inside the walls
	b.box(Vector3(0, 0.004, 0), Vector3(half * 2.0 + 0.04, 0.012, half * 2.0 + 0.04), EARTH)
	# curtain walls, with the gate gap in the front (+Z) side
	var gap := 0.055
	_curtain(b, Vector3(-half, 0, -half), Vector3(half, 0, -half), wh, 0.04, STONE, STONE_LIGHT, stage >= 3)
	_curtain(b, Vector3(-half, 0, -half), Vector3(-half, 0, half), wh, 0.04, STONE, STONE_LIGHT, stage >= 3)
	_curtain(b, Vector3(half, 0, -half), Vector3(half, 0, half), wh, 0.04, STONE, STONE_LIGHT, stage >= 3)
	_curtain(b, Vector3(-half, 0, half), Vector3(-gap, 0, half), wh, 0.04, STONE, STONE_LIGHT, stage >= 3)
	_curtain(b, Vector3(gap, 0, half), Vector3(half, 0, half), wh, 0.04, STONE, STONE_LIGHT, stage >= 3)
	# corner towers
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			_tower_at(b, Vector3(sx * half, 0, sz * half), 0.05, th, stage == 4, -sz)
	# the gate: two small gate towers and a timber door, once the walls are up
	if stage >= 3:
		for sx in [-1.0, 1.0]:
			_tower_at(b, Vector3(sx * (gap + 0.012), 0, half), 0.026, wh + 0.03, stage == 4)
		b.box(Vector3(0, 0.03, half), Vector3(gap * 2.0 - 0.02, 0.06, 0.006), TIMBER)
	# the keep: a tall stone block, a smaller upper storey, and a flat crenellated top
	var kp := Vector3(0, 0, -half * 0.28)
	b.prism(kp + Vector3(0, -0.02, 0), 0.115, 0.1, 0.02 + kh * 0.2, 4, STONE_DARK, STONE_DARK, PI / 4.0)
	b.prism(kp + Vector3(0, kh * 0.2, 0), 0.1, 0.093, kh * 0.8, 4, STONE, STONE_LIGHT, PI / 4.0)
	if stage >= 3:
		for sx in [-0.04, 0.04]:
			b.box(kp + Vector3(sx, kh * 0.55, 0.066), Vector3(0.012, 0.03, 0.004), SLIT)
		b.box(kp + Vector3(0, kh * 0.3, 0.066), Vector3(0.026, 0.04, 0.004), SLIT)
	if stage == 4:
		b.prism(kp + Vector3(0, kh, 0), 0.098, 0.098, 0.01, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)
		b.prism(kp + Vector3(0, kh + 0.01, 0), 0.07, 0.07, 0.045, 4, STONE, STONE_LIGHT, PI / 4.0)
		b.prism(kp + Vector3(0, kh + 0.055, 0), 0.085, 0.0, 0.05, 4, ROOF, ROOF, PI / 4.0)
		for k in 4:
			var a := k * PI * 0.5 + PI * 0.25
			b.box(kp + Vector3(cos(a) * 0.095, kh + 0.02, sin(a) * 0.095), Vector3(0.026, 0.016, 0.026), STONE_LIGHT)
		# the owner's banner on the keep, and a smaller one over the gate
		b.tag = CIV
		var top := kp + Vector3(0, kh + 0.1, 0)
		b.pole(top - Vector3(0, 0.03, 0), top + Vector3(0, 0.1, 0), 0.0026, TIMBER)
		b.quad(top + Vector3(0, 0.096, 0), top + Vector3(0.09, 0.08, 0), top + Vector3(0.09, 0.034, 0), top + Vector3(0, 0.05, 0), Color.WHITE,
			top + Vector3(0, 0.06, 1.0))
		b.tag = PLAIN
	else:
		# scaffolding on the front of the keep and a block-and-tackle post, stone piles and a timber stack in the bailey
		if stage >= 2:
			for sx in [-0.09, -0.03, 0.03, 0.09]:
				b.pole(kp + Vector3(sx, -0.01, 0.12), kp + Vector3(sx, kh + 0.04, 0.12), 0.0035, TIMBER_LIGHT)
			for lvl in [0.4, 0.75]:
				b.box(kp + Vector3(0, kh * lvl, 0.12), Vector3(0.21, 0.005, 0.05), TIMBER)
				b.pole(kp + Vector3(-0.09, kh * lvl, 0.12), kp + Vector3(-0.09, kh * lvl, 0.1), 0.003, TIMBER_LIGHT)
		for k in 4:
			b.blob(Vector3(-0.1 + 0.05 * k, 0.018, 0.1), Vector3(0.022, 0.014, 0.018), 5, 3, STONE_LIGHT, 0.12, STONE)
		b.box(Vector3(0.12, 0.016, 0.07), Vector3(0.07, 0.016, 0.03), TIMBER_LIGHT)
	return b.commit()
