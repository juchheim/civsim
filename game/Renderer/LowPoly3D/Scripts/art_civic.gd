## Civic buildings of the Bronze-age towns: the temple (BEL-06), and the rest as they are drawn
##
## Art pass: meshes in this file are ADDED to (or replace, by name) the ones mesh_kit.gd builds, see art_landmarks.gd. Keep: the mesh name,
## the origin (ground centre), the facing (the front, with its stair and doors, toward +Z), the footprint (world_builder.gd places each
## building on a free, level spot near the hearth with its front toward the hearth), and the tag meanings (TAG_CIV faces take the owner's
## colour, TAG_EMISSIVE glows). `kit` is the M7MeshKit: use kit._b(seed) for a builder and kit.WOOD, kit.THATCH ... for its palette.
extends RefCounted

# temple: cut stone below, warm mud-brick walls, a whitewashed cella above; every top is paler than the faces under it
const TM_STONE := Color("#8b8376")
const TM_STONE_TOP := Color("#b3a994")
const TM_TREAD := Color("#d6ccb6")
const TM_BRICK := Color("#b07e55")
const TM_BRICK_HI := Color("#c3926a")
const TM_ROOF := Color("#cfb084")
const TM_NICHE := Color("#553624")
const TM_LIME := Color("#e2d6bc")
const TM_LIME_TOP := Color("#f2ead8")
const TM_PAVING := Color("#a38c6c")
const TM_OCHRE := Color("#9c3d27")
const TM_DARK := Color("#2a2119")
const TM_GOLD := Color("#c9973c")
const TM_GLOW := Color("#9a4212")
const TM_FLAME := Color("#ff5a12")
const TM_FLAME_CORE := Color("#ffd27a")

static func build(kit) -> void:
	_temple(kit)

## A block standing on `p` (the centre of its foot) whose walls lean in from half-extents `bot` to `top` (x, z); no underside, and
## no `skip` face where it is built against something (0 the -Z face, 1 +X, 2 +Z, 3 -X).
static func _battered(b: M7MeshBuilder, p: Vector3, bot: Vector2, top: Vector2, h: float, col: Color, col_top = null, skip: int = -1) -> void:
	var ct: Color = col if col_top == null else col_top
	var lo := [Vector3(-bot.x, 0, -bot.y), Vector3(bot.x, 0, -bot.y), Vector3(bot.x, 0, bot.y), Vector3(-bot.x, 0, bot.y)]
	var hi := [Vector3(-top.x, h, -top.y), Vector3(top.x, h, -top.y), Vector3(top.x, h, top.y), Vector3(-top.x, h, top.y)]
	var mid := p + Vector3(0, h * 0.5, 0)
	for i in 4:
		if i == skip:
			continue
		var k := (i + 1) % 4
		b.quad(p + lo[i], p + lo[k], p + hi[k], p + hi[i], col, mid)
	b.quad(p + hi[0], p + hi[1], p + hi[2], p + hi[3], ct, mid)

## A plain upright block on `p` (the centre of its foot), full extents `size`; no underside.
static func _block(b: M7MeshBuilder, p: Vector3, size: Vector3, col: Color, col_top = null, skip: int = -1) -> void:
	var hz := Vector2(size.x * 0.5, size.z * 0.5)
	_battered(b, p, hz, hz, size.y, col, col_top, skip)

## The four side faces of a rectangular band round (cx, cz) with half-extents `half`, from y up by h, no caps.
static func _belt(b: M7MeshBuilder, cx: float, cz: float, half: Vector2, y: float, h: float, col: Color) -> void:
	var cs := [Vector3(cx - half.x, y, cz - half.y), Vector3(cx + half.x, y, cz - half.y), Vector3(cx + half.x, y, cz + half.y), Vector3(cx - half.x, y, cz + half.y)]
	var up := Vector3(0, h, 0)
	for i in 4:
		var p0: Vector3 = cs[i]
		var p1: Vector3 = cs[(i + 1) % 4]
		b.quad(p0, p1, p1 + up, p0 + up, col, Vector3(cx, y + h * 0.5, cz))

## A small fire: an outer tongue and a pale core.
static func _flame(b: M7MeshBuilder, p: Vector3, r: float, h: float) -> void:
	var save := b.tag
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	b.prism(p, r, 0.0, h, 5, TM_FLAME, TM_FLAME, 0.3)
	b.prism(p + Vector3(0, 0.001, 0), r * 0.55, 0.0, h * 0.75, 4, TM_FLAME_CORE, TM_FLAME_CORE)
	b.tag = save

# ------------------------------------------------------------------ temple (BEL-06)

static func _temple(kit) -> void:
	# a long house of the god rather than a tower: a cut-stone plinth with a front stair, a buttressed mud-brick hall with dark
	# niches, a whitewashed cella raised above it with clerestory slits and gilded horns, a pylon gate with a red-pillared porch
	# under a civ awning, civ friezes and streamers on tall poles, and a walled forecourt with a horned altar and fire-topped
	# gate posts out front (+Z)
	var b: M7MeshBuilder = kit._b(810)
	# plinth: a battered stone base sunk into the ground with a paler, slightly proud coping, and a three-step stair
	var pz := -0.0675
	_battered(b, Vector3(0, -0.02, pz), Vector2(0.152, 0.139), Vector2(0.147, 0.134), 0.04, TM_STONE, TM_STONE_TOP)
	_block(b, Vector3(0, 0.018, pz), Vector3(0.3, 0.009, 0.272), TM_STONE_TOP.darkened(0.08), TM_STONE_TOP)
	for k in 3:
		var zf := 0.116 - 0.0145 * k
		_block(b, Vector3(0, -0.012, (zf + 0.06) * 0.5), Vector3(0.096 - 0.008 * k, 0.012 + 0.0093 * (k + 1), zf - 0.06), TM_STONE, TM_TREAD, 0)
	# the hall: mud-brick walls with buttresses and dark niches between them, a pale flat roof, a civ frieze under the eaves
	var floor_y := 0.027
	var hz := -0.095
	var hx := 0.122
	_block(b, Vector3(0, floor_y, hz), Vector3(hx * 2.0, 0.078, 0.19), TM_BRICK, TM_ROOF)
	for sx in [-1.0, 1.0]:
		for z in [-0.17, -0.098, -0.026]:
			_block(b, Vector3(sx * (hx + 0.003), floor_y, z), Vector3(0.008, 0.066, 0.014), TM_BRICK_HI, null, 3 if sx > 0.0 else 1)
		for z in [-0.134, -0.062]:
			var x: float = sx * (hx + 0.0006)
			b.quad(Vector3(x, 0.04, z - 0.0045), Vector3(x, 0.04, z + 0.0045), Vector3(x, 0.083, z + 0.0045), Vector3(x, 0.083, z - 0.0045), TM_NICHE, Vector3(0, 0.06, hz))
	for x in [-0.08, -0.04, 0.0, 0.04, 0.08]:
		b.quad(Vector3(x - 0.0045, 0.04, -0.1906), Vector3(x + 0.0045, 0.04, -0.1906), Vector3(x + 0.0045, 0.083, -0.1906), Vector3(x - 0.0045, 0.083, -0.1906), TM_NICHE, Vector3(0, 0.06, hz))
	b.tag = M7MeshBuilder.TAG_CIV
	_belt(b, 0.0, hz, Vector2(hx + 0.0012, 0.0962), 0.093, 0.009, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	# the cella: the god's room raised over the back of the hall, whitewashed, with clerestory slits and a civ frieze
	var cz := -0.108
	var top_y := 0.105
	_block(b, Vector3(0, top_y, cz), Vector3(0.136, 0.056, 0.13), TM_LIME, TM_LIME_TOP)
	for sx in [-1.0, 1.0]:
		var x: float = sx * 0.0686
		for z in [-0.13, -0.086]:
			b.quad(Vector3(x, 0.119, z - 0.007), Vector3(x, 0.119, z + 0.007), Vector3(x, 0.139, z + 0.007), Vector3(x, 0.139, z - 0.007), TM_DARK, Vector3(0, 0.13, cz))
	for x in [-0.036, 0.0, 0.036]:
		b.quad(Vector3(x - 0.007, 0.119, -0.0426), Vector3(x + 0.007, 0.119, -0.0426), Vector3(x + 0.007, 0.139, -0.0426), Vector3(x - 0.007, 0.139, -0.0426), TM_DARK, Vector3(0, 0.13, cz))
	b.tag = M7MeshBuilder.TAG_CIV
	_belt(b, 0.0, cz, Vector2(0.0692, 0.0662), 0.146, 0.008, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	# gilded crescent horns on the cella's front edge
	var fy := top_y + 0.056
	var fz := -0.06
	for sx in [-1.0, 1.0]:
		var p0 := Vector3(sx * 0.003, fy, fz)
		var p1 := Vector3(sx * 0.016, fy + 0.016, fz)
		var p2 := Vector3(sx * 0.009, fy + 0.034, fz)
		b.pole(p0, p1, 0.0026, TM_GOLD, 3)
		b.pole(p1, p2, 0.0021, TM_GOLD, 3)
	# the pylon gate: two battered towers with a stone cornice over a civ band, a dark slot down each face for its pole
	var gz := 0.022
	var g_bot := Vector2(0.038, 0.03)
	var g_top := Vector2(0.032, 0.025)
	var g_h := 0.12
	for sx in [-1.0, 1.0]:
		var gx: float = sx * 0.085
		_battered(b, Vector3(gx, floor_y, gz), g_bot, g_top, g_h, TM_BRICK_HI, TM_ROOF, 0)
		var band_y := floor_y + g_h - 0.014
		var t := (band_y - floor_y) / g_h
		var band_half := g_bot.lerp(g_top, t) + Vector2(0.0012, 0.0012)
		b.tag = M7MeshBuilder.TAG_CIV
		_belt(b, gx, gz, band_half, band_y, 0.01, Color.WHITE)
		b.tag = M7MeshBuilder.TAG_PLAIN
		_block(b, Vector3(gx, floor_y + g_h, gz), Vector3(g_top.x * 2.0 + 0.008, 0.011, g_top.y * 2.0 + 0.008), TM_STONE_TOP.darkened(0.06), TM_LIME_TOP)
		var ya := floor_y + 0.012
		var yb := floor_y + g_h - 0.022
		var za := gz + g_bot.y + (g_top.y - g_bot.y) * (ya - floor_y) / g_h + 0.0007
		var zb := gz + g_bot.y + (g_top.y - g_bot.y) * (yb - floor_y) / g_h + 0.0007
		b.quad(Vector3(gx - 0.0055, ya, za), Vector3(gx + 0.0055, ya, za), Vector3(gx + 0.0055, yb, zb), Vector3(gx - 0.0055, yb, zb), TM_NICHE, Vector3(gx, ya, gz))
		# a tall pole before the slot with a long civ streamer flying outward
		var pole_z := gz + g_bot.y + 0.005
		b.pole(Vector3(gx, floor_y, pole_z), Vector3(gx, 0.282, pole_z), 0.003, M7MeshKit.WOOD, 3)
		b.tag = M7MeshBuilder.TAG_CIV
		var y := 0.276
		var s0 := Vector3(gx, y, pole_z)
		var s1 := Vector3(gx + sx * 0.05, y - 0.008, pole_z)
		var s2 := Vector3(gx + sx * 0.036, y - 0.013, pole_z)
		var s3 := Vector3(gx + sx * 0.052, y - 0.022, pole_z)
		var s4 := Vector3(gx, y - 0.02, pole_z)
		var away := Vector3(gx, y, pole_z - 0.3)
		b.tri(s0, s1, s2, Color.WHITE, away)
		b.tri(s0, s2, s4, Color.WHITE, away)
		b.tri(s2, s3, s4, Color.WHITE, away)
		b.tag = M7MeshBuilder.TAG_PLAIN
	# the porch between the towers: two red pillars on stone bases under a stone lintel and a civ awning, the dark doorway
	# behind with the god's lamp glowing low inside
	for sx in [-1.0, 1.0]:
		_block(b, Vector3(sx * 0.024, floor_y, 0.04), Vector3(0.016, 0.007, 0.016), TM_STONE_TOP.darkened(0.05), TM_STONE_TOP)
		_block(b, Vector3(sx * 0.024, floor_y + 0.007, 0.04), Vector3(0.009, 0.056, 0.009), TM_OCHRE)
	_block(b, Vector3(0, floor_y + 0.063, 0.024), Vector3(0.104, 0.01, 0.044), TM_STONE_TOP.darkened(0.06), TM_STONE_TOP, 0)
	b.tag = M7MeshBuilder.TAG_CIV
	b.quad(Vector3(-0.052, 0.107, -0.002), Vector3(0.052, 0.107, -0.002), Vector3(0.052, 0.1, 0.058), Vector3(-0.052, 0.1, 0.058), Color.WHITE, Vector3(0, 0.0, 0.03))
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.quad(Vector3(-0.022, floor_y, 0.0006), Vector3(0.022, floor_y, 0.0006), Vector3(0.022, 0.084, 0.0006), Vector3(-0.022, 0.084, 0.0006), TM_DARK, Vector3(0, 0.05, -0.05))
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	b.quad(Vector3(-0.012, floor_y + 0.001, 0.0012), Vector3(0.012, floor_y + 0.001, 0.0012), Vector3(0.008, floor_y + 0.016, 0.0012), Vector3(-0.008, floor_y + 0.016, 0.0012), TM_GLOW, Vector3(0, 0.05, -0.05))
	b.tag = M7MeshBuilder.TAG_PLAIN
	# the forecourt: paving at ground level inside a low mud-brick wall, a gate of two stone posts with fires burning on top
	var court_z := 0.1445
	_block(b, Vector3(0, -0.02, court_z), Vector3(0.288, 0.024, 0.141), TM_PAVING.darkened(0.15), TM_PAVING, 0)
	for sx in [-1.0, 1.0]:
		_block(b, Vector3(sx * 0.143, -0.02, court_z), Vector3(0.012, 0.05, 0.145), TM_BRICK, TM_ROOF, 0)
		_block(b, Vector3(sx * 0.095, -0.02, 0.209), Vector3(0.1, 0.05, 0.012), TM_BRICK, TM_ROOF)
		var gp := Vector3(sx * 0.038, -0.02, 0.209)
		_block(b, gp, Vector3(0.018, 0.078, 0.018), TM_STONE, TM_DARK)
		_flame(b, gp + Vector3(0, 0.078, 0), 0.008, 0.024)
	# the altar: a whitewashed block with a horn at each corner and the offering fire on top, an offering table beside it
	var az := 0.138
	_block(b, Vector3(0, 0.004, az), Vector3(0.042, 0.026, 0.03), TM_LIME, TM_LIME_TOP)
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			var c := Vector3(sx * 0.0175, 0.03, az + sz * 0.0122)
			b.tri(c + Vector3(-sx * 0.0045, 0, 0), c + Vector3(0, 0, -sz * 0.0045), c + Vector3(0, 0.009, 0), TM_LIME_TOP, Vector3(0, 0.02, az))
	_flame(b, Vector3(0, 0.03, az), 0.013, 0.042)
	_block(b, Vector3(0.066, 0.004, az + 0.004), Vector3(0.03, 0.014, 0.018), M7MeshKit.WOOD, M7MeshKit.WOOD_LIGHT)
	b.prism(Vector3(0.06, 0.018, az + 0.004), 0.0055, 0.0035, 0.011, 5, M7MeshKit.MUD, TM_DARK)
	kit.meshes["temple"] = b.commit()
