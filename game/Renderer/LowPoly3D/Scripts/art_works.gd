## Iron Age civic works: the academy (LRN-05), the theatre or arena (ART-08), the bathhouse (MED-06), the treadwheel crane (MEC-06), the aqueduct (BLD-10) and
## the building site that stands where one of them is going up.
##
## Laid out by world_builder.gd (`_civic_works`, `_aqueduct`, `_build_site`). Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.1 tall).
## Buildings face +Z (their front, toward the hearth); TAG_CIV faces take the owner's colour.
##
##   academy          a limewashed hall on a stone podium behind a columned portico and pediment, a frieze in the owner's colour
##   amphitheatre     a ring of arched outer wall with tiers of seats stepping down to a sand floor: the theatre or arena
##   bathhouse        a low block with a vaulted roof, a tall chimney and a tiled porch
##   crane            a timber mast and jib over a man-sized treadwheel, a rope and a stone on its hook, blocks stacked beside it
##   aqueduct_run     one 0.3 long span of channel carried on arches, deck 0.14 up (the builder stretches it to level ground); runs along local X
##   aqueduct_end     the water tower where the aqueduct comes into the village: a basin on a stone pier with a spout
##   build_site_1..3  foundations, walls rising on scaffolding, walls up with roof timbers on
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const LIME := Color("#e2d6bc")
const LIME_TOP := Color("#f2ead8")
const SAND := Color("#d8c08e")
const ROOF := Color("#a8492c")
const ROOF_DARK := Color("#7f3622")
const ARCH := Color("#2a2119")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const ROPE := Color("#b9a68a")
const WATER := Color("#4a86a8")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV

static func build(kit) -> void:
	kit.meshes["academy"] = _academy(kit)
	kit.meshes["amphitheatre"] = _amphitheatre(kit)
	kit.meshes["bathhouse"] = _bathhouse(kit)
	kit.meshes["crane"] = _crane(kit)
	kit.meshes["aqueduct_run"] = _aqueduct_run(kit)
	kit.meshes["aqueduct_end"] = _aqueduct_end(kit)
	for stage in range(1, 4):
		kit.meshes["build_site_%d" % stage] = _site(kit, stage)

# ------------------------------------------------------------------ pieces

## A gable roof over a box of half-extents (hx, hz) with its eaves at `y`, ridge along X, `rise` high; tiles in two shades.
static func _gable(b: M7MeshBuilder, hx: float, hz: float, y: float, rise: float, col: Color) -> void:
	var inner := Vector3(0, y - 0.01, 0)
	for side in [-1.0, 1.0]:
		var c := col if side > 0.0 else col.darkened(0.12)
		b.quad(Vector3(-hx, y, hz * side), Vector3(hx, y, hz * side), Vector3(hx, y + rise, 0), Vector3(-hx, y + rise, 0), c, inner)
	for sx in [-1.0, 1.0]:
		b.tri(Vector3(sx * hx, y, hz), Vector3(sx * hx, y, -hz), Vector3(sx * hx, y + rise, 0), ROOF_DARK, inner)

static func _column(b: M7MeshBuilder, x: float, z: float, h: float) -> void:
	b.prism(Vector3(x, 0.0, z), 0.012, 0.01, h, 7, LIME, LIME_TOP)
	b.prism(Vector3(x, h, z), 0.015, 0.015, 0.008, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)
	b.prism(Vector3(x, -0.002, z), 0.016, 0.016, 0.008, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)

## A ring wall: the side faces of a polygon between radii at height y0..y1 (outer faces facing out unless `inward`), plus its top as an annulus.
static func _ring(b: M7MeshBuilder, r0: float, r1: float, y0: float, y1: float, col: Color, col_top: Color, sides: int) -> void:
	for i in sides:
		var a0 := TAU * i / sides
		var a1 := TAU * (i + 1) / sides
		var o0 := Vector3(cos(a0) * r1, 0, sin(a0) * r1)
		var o1 := Vector3(cos(a1) * r1, 0, sin(a1) * r1)
		var n0 := Vector3(cos(a0) * r0, 0, sin(a0) * r0)
		var n1 := Vector3(cos(a1) * r0, 0, sin(a1) * r0)
		b.quad(o0 + Vector3(0, y0, 0), o1 + Vector3(0, y0, 0), o1 + Vector3(0, y1, 0), o0 + Vector3(0, y1, 0), col, Vector3(0, y1 * 0.5, 0))
		b.quad(n0 + Vector3(0, y1, 0), n1 + Vector3(0, y1, 0), o1 + Vector3(0, y1, 0), o0 + Vector3(0, y1, 0), col_top, Vector3(0, -1, 0))

# ------------------------------------------------------------------ academy

static func _academy(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(980)
	b.tag = PLAIN
	var hx := 0.2
	var hz := 0.14
	# podium with steps, the hall body behind the portico, columns, an entablature and a pediment
	b.box(Vector3(0, 0.014, 0.01), Vector3(hx * 2.0 + 0.04, 0.028, hz * 2.0 + 0.06), STONE, STONE_LIGHT)
	b.box(Vector3(0, 0.006, hz + 0.075), Vector3(0.2, 0.012, 0.03), STONE_LIGHT)
	b.box(Vector3(0, 0.07, -0.02), Vector3(hx * 2.0 - 0.04, 0.084, hz * 2.0 - 0.06), LIME, LIME_TOP)
	for k in 6:
		var x := -0.16 + 0.064 * k
		_column(b, x, hz + 0.02, 0.092)
	b.box(Vector3(0, 0.104, hz + 0.02), Vector3(hx * 2.0 - 0.02, 0.016, 0.05), STONE_LIGHT)
	b.tag = CIV
	b.box(Vector3(0, 0.118, hz + 0.02), Vector3(hx * 2.0 - 0.02, 0.012, 0.052), Color.WHITE)
	b.tag = PLAIN
	# the roof: a gable with a front pediment over the portico
	_gable(b, hx - 0.02, hz - 0.02, 0.112, 0.07, ROOF)
	b.tri(Vector3(-hx + 0.01, 0.124, hz + 0.046), Vector3(hx - 0.01, 0.124, hz + 0.046), Vector3(0, 0.178, hz + 0.046), LIME, Vector3(0, 0.1, -0.1))
	b.tri(Vector3(-hx + 0.02, 0.124, hz + 0.046), Vector3(hx - 0.02, 0.124, hz + 0.046), Vector3(0, 0.166, hz + 0.047), LIME_TOP, Vector3(0, 0.1, -0.1))
	# door and windows
	b.box(Vector3(0, 0.052, hz - 0.005), Vector3(0.04, 0.056, 0.004), ARCH)
	for wx in [-0.12, 0.12]:
		b.box(Vector3(wx, 0.07, hz - 0.005), Vector3(0.026, 0.034, 0.004), ARCH)
	return b.commit()

# ------------------------------------------------------------------ theatre / arena

static func _amphitheatre(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(981)
	b.tag = PLAIN
	var sides := 18
	# the outer wall with arched openings, then seats stepping down in four tiers to the sand
	_ring(b, 0.27, 0.31, -0.03, 0.105, STONE, STONE_LIGHT, sides)
	_ring(b, 0.23, 0.27, -0.03, 0.088, STONE_LIGHT, STONE, sides)
	_ring(b, 0.19, 0.23, -0.03, 0.07, STONE, STONE_LIGHT, sides)
	_ring(b, 0.155, 0.19, -0.03, 0.052, STONE_LIGHT, STONE, sides)
	b.prism(Vector3(0, 0.0, 0), 0.158, 0.158, 0.012, sides, SAND, SAND.lightened(0.05))
	# the arches round the outer face
	for i in sides:
		var a := TAU * (i + 0.5) / sides
		var p := Vector3(cos(a) * 0.312, 0.035, sin(a) * 0.312)
		var save := b.xf
		b.xf = b.xf * Transform3D(Basis(Vector3.UP, -a + PI * 0.5), p)
		b.box(Vector3.ZERO, Vector3(0.05, 0.06, 0.012), ARCH)
		b.box(Vector3(0, 0.036, 0), Vector3(0.056, 0.01, 0.014), STONE_LIGHT)
		b.xf = save
	# banners on poles at the two entrances, in the owner's colour
	b.tag = CIV
	for sx in [-1.0, 1.0]:
		var top := Vector3(sx * 0.29, 0.105, 0.0)
		b.pole(top, top + Vector3(0, 0.09, 0), 0.0024, WOOD)
		b.quad(top + Vector3(0, 0.088, 0), top + Vector3(0, 0.07, 0.044), top + Vector3(0, 0.044, 0.044), top + Vector3(0, 0.058, 0), Color.WHITE, top + Vector3(1, 0.05, 0))
	b.tag = PLAIN
	return b.commit()

# ------------------------------------------------------------------ bathhouse

static func _bathhouse(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(982)
	b.tag = PLAIN
	var hx := 0.13
	var hz := 0.1
	b.box(Vector3(0, 0.012, 0), Vector3(hx * 2.0 + 0.02, 0.024, hz * 2.0 + 0.02), STONE_DARK, STONE)
	b.box(Vector3(0, 0.06, 0), Vector3(hx * 2.0, 0.072, hz * 2.0), LIME, LIME_TOP)
	# a vaulted roof: a half-cylinder of tiles along X
	var steps := 7
	for i in steps:
		var a0 := PI * i / steps
		var a1 := PI * (i + 1) / steps
		var y0 := 0.096 + sin(a0) * 0.05
		var y1 := 0.096 + sin(a1) * 0.05
		var z0 := -cos(a0) * (hz + 0.006)
		var z1 := -cos(a1) * (hz + 0.006)
		var col := ROOF if i % 2 == 0 else ROOF.darkened(0.1)
		b.quad(Vector3(-hx - 0.006, y0, z0), Vector3(hx + 0.006, y0, z0), Vector3(hx + 0.006, y1, z1), Vector3(-hx - 0.006, y1, z1), col, Vector3(0, 0.08, 0))
	for sx in [-1.0, 1.0]:
		b.tri(Vector3(sx * (hx + 0.006), 0.096, -hz - 0.006), Vector3(sx * (hx + 0.006), 0.096, hz + 0.006), Vector3(sx * (hx + 0.006), 0.146, 0), ROOF_DARK, Vector3(0, 0.08, 0))
	# the furnace chimney and a tiled porch with two columns
	b.prism(Vector3(-0.08, 0.1, -0.05), 0.016, 0.013, 0.1, 5, STONE, STONE_LIGHT)
	b.box(Vector3(0, 0.044, hz - 0.002), Vector3(0.036, 0.05, 0.004), ARCH)
	b.box(Vector3(0, 0.07, hz + 0.034), Vector3(0.1, 0.01, 0.06), ROOF)
	for sx in [-0.04, 0.04]:
		_column(b, sx, hz + 0.054, 0.062)
	return b.commit()

# ------------------------------------------------------------------ crane

static func _crane(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(983)
	b.tag = PLAIN
	# the treadwheel: a rim of planks with spokes, standing on its edge beside the mast, with steps inside it
	var wc := Vector3(0, 0.075, -0.05)
	for i in 12:
		var a0 := TAU * i / 12.0
		var a1 := TAU * (i + 1) / 12.0
		var p0 := wc + Vector3(0, sin(a0) * 0.07, cos(a0) * 0.07)
		var p1 := wc + Vector3(0, sin(a1) * 0.07, cos(a1) * 0.07)
		for sx in [-0.026, 0.026]:
			b.pole(p0 + Vector3(sx, 0, 0), p1 + Vector3(sx, 0, 0), 0.006, WOOD_LIGHT)
		b.pole(p0 + Vector3(-0.026, 0, 0), p0 + Vector3(0.026, 0, 0), 0.004, WOOD)
	for a in [0.0, PI / 3.0, 2.0 * PI / 3.0]:
		for sx in [-0.026, 0.026]:
			b.pole(wc + Vector3(sx, sin(a) * 0.07, cos(a) * 0.07), wc + Vector3(sx, -sin(a) * 0.07, -cos(a) * 0.07), 0.003, WOOD)
	b.pole(wc + Vector3(-0.04, 0, 0), wc + Vector3(0.04, 0, 0), 0.006, WOOD_LIGHT)
	# two uprights frame the wheel, and the mast rises behind it with a braced jib out over the work
	for sx in [-0.04, 0.04]:
		b.pole(Vector3(sx, 0.0, -0.05), Vector3(sx, 0.16, -0.05), 0.007, WOOD)
	b.pole(Vector3(0, 0.0, 0.03), Vector3(0, 0.3, 0.03), 0.01, WOOD_LIGHT)
	b.pole(Vector3(0, 0.27, 0.03), Vector3(0, 0.3, 0.2), 0.007, WOOD_LIGHT)
	b.pole(Vector3(0, 0.2, 0.03), Vector3(0, 0.285, 0.14), 0.004, WOOD)
	b.pole(Vector3(0, 0.27, 0.03), Vector3(0, 0.25, -0.05), 0.003, ROPE)
	# rope and a block of stone on the hook, a pile of cut blocks beside the mast
	b.pole(Vector3(0, 0.3, 0.19), Vector3(0, 0.14, 0.19), 0.0022, ROPE)
	b.box(Vector3(0, 0.125, 0.19), Vector3(0.05, 0.034, 0.04), STONE_LIGHT, STONE)
	for i in 3:
		b.box(Vector3(0.08, 0.012 + 0.024 * (i / 2), 0.06 + 0.052 * (i % 2)), Vector3(0.05, 0.024, 0.04), STONE, STONE_LIGHT)
	return b.commit()

# ------------------------------------------------------------------ aqueduct

static func _aqueduct_run(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(984)
	b.tag = PLAIN
	var half := 0.15
	var w := 0.036
	var top := 0.14
	# piers at both ends and the middle, with two arches between them, a channel on top between low walls
	for px in [-half + 0.022, 0.0, half - 0.022]:
		b.box(Vector3(px, (top - 0.05) * 0.5 - 0.03, 0), Vector3(0.044, top - 0.05 + 0.06, w * 2.0), STONE, STONE_LIGHT)
	b.box(Vector3(0, top - 0.027, 0), Vector3(half * 2.0, 0.054, w * 2.0), STONE, STONE_LIGHT)
	# the two openings are left clear (see-through); a pale keystone course tops each
	for ax in [-half * 0.5, half * 0.5]:
		for sz in [-1.0, 1.0]:
			b.box(Vector3(ax, top - 0.058, sz * (w + 0.001)), Vector3(0.1, 0.008, 0.004), STONE_DARK)
	for sz in [-1.0, 1.0]:
		b.box(Vector3(0, top + 0.012, sz * (w - 0.007)), Vector3(half * 2.0, 0.024, 0.012), STONE_LIGHT)
	b.box(Vector3(0, top + 0.004, 0), Vector3(half * 2.0, 0.006, w * 2.0 - 0.02), WATER)
	return b.commit()

static func _aqueduct_end(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(985)
	b.tag = PLAIN
	# a stone pier carrying a basin, where the channel arrives (from +X), and a spout into a trough at its foot
	b.box(Vector3(0, 0.07, 0), Vector3(0.1, 0.14, 0.1), STONE, STONE_LIGHT)
	b.box(Vector3(0, 0.15, 0), Vector3(0.14, 0.03, 0.14), STONE_LIGHT)
	b.box(Vector3(0, 0.168, 0), Vector3(0.12, 0.012, 0.12), WATER)
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * 0.064, 0.17, 0), Vector3(0.012, 0.024, 0.14), STONE_LIGHT)
	b.pole(Vector3(-0.05, 0.1, 0), Vector3(-0.09, 0.07, 0), 0.006, STONE_DARK)
	b.box(Vector3(-0.1, 0.012, 0), Vector3(0.07, 0.024, 0.05), STONE_DARK, STONE)
	b.box(Vector3(-0.1, 0.026, 0), Vector3(0.06, 0.004, 0.04), WATER)
	return b.commit()

# ------------------------------------------------------------------ building site

static func _site(kit, stage: int) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(990 + stage)
	b.tag = PLAIN
	var hx := 0.15
	var hz := 0.11
	var wall: float = [0.0, 0.0, 0.05, 0.1][stage]
	b.box(Vector3(0, 0.004, 0), Vector3(hx * 2.0 + 0.06, 0.01, hz * 2.0 + 0.06), Color("#7b6247"))
	# foundation courses, and walls where they have risen
	b.box(Vector3(0, 0.016, 0), Vector3(hx * 2.0, 0.018, hz * 2.0), STONE_DARK, STONE)
	if wall > 0.0:
		for sz in [-1.0, 1.0]:
			b.box(Vector3(0, 0.025 + wall * 0.5, sz * (hz - 0.012)), Vector3(hx * 2.0, wall, 0.024), STONE, STONE_LIGHT)
		for sx in [-1.0, 1.0]:
			b.box(Vector3(sx * (hx - 0.012), 0.025 + wall * 0.5, 0), Vector3(0.024, wall, hz * 2.0 - 0.04), STONE, STONE_LIGHT)
	# scaffolding round the work: poles, ledgers and a plank at each lift
	if stage >= 2:
		var lifts := [0.04] if stage == 2 else [0.04, 0.08, 0.12]
		for sx in [-0.17, -0.057, 0.057, 0.17]:
			b.pole(Vector3(sx, -0.01, hz + 0.04), Vector3(sx, 0.07 + 0.06 * (stage - 1), hz + 0.04), 0.0032, WOOD_LIGHT)
		for y in lifts:
			b.box(Vector3(0, y, hz + 0.04), Vector3(0.36, 0.005, 0.034), WOOD)
	# roof timbers laid across the finished walls
	if stage == 3:
		for k in 5:
			var x := -0.12 + 0.06 * k
			b.pole(Vector3(x, 0.125, -hz), Vector3(x, 0.155, 0.0), 0.004, WOOD_LIGHT)
			b.pole(Vector3(x, 0.125, hz), Vector3(x, 0.155, 0.0), 0.004, WOOD_LIGHT)
	# stone and timber stacks beside the site and a sled of blocks
	for i in 4:
		b.blob(Vector3(-0.2 + 0.034 * i, 0.016, 0.17), Vector3(0.022, 0.014, 0.018), 5, 3, STONE_LIGHT, 0.12, STONE)
	b.box(Vector3(0.2, 0.014, 0.12), Vector3(0.08, 0.028, 0.034), WOOD_LIGHT)
	b.box(Vector3(0.2, 0.034, 0.12), Vector3(0.07, 0.012, 0.03), WOOD)
	return b.commit()
