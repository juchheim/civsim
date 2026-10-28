## Landmarks: temple, chief's hall, fleece bales and the megalith pieces
##
## Art pass: meshes in this file REPLACE the ones mesh_kit.gd/looks_kit.gd build under the same names (`kit.meshes[name] = ...`), so
## nothing that places them changes. Keep: the mesh name, the origin (ground centre), the facing (doors/fronts toward +Z), the footprint
## (the village layout spaces dwellings by DWELL_RADIUS in world_builder.gd), and the tag meanings (TAG_CIV faces take the owner's
## colour, TAG_EMISSIVE glows). `kit` is the M7MeshKit: use kit._b(seed) for a builder and kit.WOOD, kit.THATCH ... for its palette.
extends RefCounted

# temple: each terrace's wall is darker than the walkway on top of it, and the terraces get paler as they rise
const T_WALL := [Color("#8a5f43"), Color("#a6774f"), Color("#c09468")]
const T_WALK := [Color("#c6a074"), Color("#d6b688"), Color("#e6cfa2")]
const T_NICHE := Color("#4f3424")
const T_TREAD := Color("#eddcb6")
const T_RISER := Color("#9c7655")
const SHRINE_RED := Color("#a8432c")
const LIME := Color("#efe6d0")
const GLAZE := Color("#1f8f8c")
const GLAZE_HI := Color("#46b8aa")
const GLAZE_DARK := Color("#15605d")
const GOLD := Color("#c9973c")
const BRONZE := Color("#8c5a2b")
const DARK := Color("#2a2119")
const FLAME := Color("#ff5a12")
const FLAME_CORE := Color("#ffd27a")
# chief's hall
const KERB := Color("#77716a")
const YARD := Color("#9c7d58")
const PLANK := Color("#5a3f2a")
const HORN := Color("#e8dcc0")
const GLOW := Color("#8a3a10")
const CREAM := Color("#e8dcc0")
# fleece and the megalith
const FLEECE := Color("#efe8d6")
const FLEECE_CREAM := Color("#e0d2b2")
const FLEECE_BROWN := Color("#8a6a4a")
const FLEECE_DARK := Color("#4a3a30")
const CORD := Color("#6a4a30")
const OCHRE := Color("#a4452c")
const CHALK := Color("#ddd6c4")
const LICHEN := Color("#8e9670")
const STONE_FOOT := Color("#6f695f")
const ROPE := Color("#b9a68a")
const CUT_WOOD := Color("#d2b48a")

static func build(kit) -> void:
	_temple(kit)
	_chief_hall(kit)
	_wool_bales(kit)
	_megalith(kit)

## Runs the builder with its transform set to `xf`, then puts the old one back.
static func _with(b: M7MeshBuilder, xf: Transform3D) -> Transform3D:
	var save := b.xf
	b.xf = save * xf
	return save

## The four side faces of a square band (corners on the diagonals at radius `r`, like prism(..., 4, ..., PI / 4)), no caps.
static func _band(b: M7MeshBuilder, y: float, h: float, r: float, col: Color) -> void:
	for i in 4:
		var a0 := PI * 0.25 + PI * 0.5 * i
		var a1 := a0 + PI * 0.5
		var p0 := Vector3(cos(a0) * r, y, sin(a0) * r)
		var p1 := Vector3(cos(a1) * r, y, sin(a1) * r)
		b.quad(p0, p1, p1 + Vector3(0, h, 0), p0 + Vector3(0, h, 0), col, Vector3(0, y + h * 0.5, 0))

# ------------------------------------------------------------------ temple (BEL-07)

static func _temple(kit) -> void:
	# three battered mud-brick terraces with dark niches and civ-painted parapets, a striped stair up the front (+Z), a red shrine
	# with a white portico under a glazed turquoise roof, braziers at the stair head and swallowtail pennants on corner buttresses
	var b: M7MeshBuilder = kit._b(780)
	var tiers := [[0.118, 0.108, 0.0, 0.048], [0.092, 0.082, 0.048, 0.044], [0.066, 0.058, 0.092, 0.04]]
	for k in 3:
		var t: Array = tiers[k]
		b.prism(Vector3(0, float(t[2]), 0), float(t[0]), float(t[1]), float(t[3]), 4, T_WALL[k], T_WALK[k], PI * 0.25)
	# niches on the two lower terraces (none behind the stair)
	for k in 2:
		var t: Array = tiers[k]
		var y0 := float(t[2])
		var h := float(t[3])
		var hb := float(t[0]) * 0.7071
		var ht := float(t[1]) * 0.7071
		var xs := [-0.055, -0.033, -0.011, 0.011, 0.033, 0.055] if k == 0 else [-0.04, -0.02, 0.0, 0.02, 0.04]
		for face in 4:
			var save := _with(b, Transform3D(Basis(Vector3.UP, PI * 0.5 * face), Vector3.ZERO))
			for xc in xs:
				if face == 0 and absf(xc) < 0.03:
					continue
				var ya := y0 + 0.007
				var yb := y0 + h - 0.011
				var za := hb + (ht - hb) * (ya - y0) / h + 0.0007
				var zb := hb + (ht - hb) * (yb - y0) / h + 0.0007
				b.quad(Vector3(xc - 0.0045, ya, za), Vector3(xc + 0.0045, ya, za), Vector3(xc + 0.0045, yb, zb), Vector3(xc - 0.0045, yb, zb), T_NICHE, Vector3(0, y0, 0))
			b.xf = save
	# corner buttresses on the lowest terrace, a little proud of it
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			b.box(Vector3(sx * 0.078, 0.027, sz * 0.078), Vector3(0.017, 0.054, 0.017), T_WALL[0].darkened(0.08), T_WALK[0])
	# painted parapets: the civilization's colour round the lip of the two lower terraces
	b.tag = M7MeshBuilder.TAG_CIV
	_band(b, 0.041, 0.007, 0.1092, Color.WHITE)
	_band(b, 0.0855, 0.0065, 0.0835, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	# the stair: pale treads, darker risers, between sloping balustrades
	var n := 11
	var z_front := 0.104
	var z_back := 0.041
	var d := (z_front - z_back) / n
	var sw := 0.016
	for i in n:
		var y0 := 0.132 * i / n
		var y1 := 0.132 * (i + 1) / n
		var zf := z_front - d * i
		var zb := zf - d
		var mid := Vector3(0, y1 * 0.5, zb)
		b.quad(Vector3(-sw, y1, zf), Vector3(sw, y1, zf), Vector3(sw, y1, zb), Vector3(-sw, y1, zb), T_TREAD, mid)
		b.quad(Vector3(-sw, y0, zf), Vector3(sw, y0, zf), Vector3(sw, y1, zf), Vector3(-sw, y1, zf), T_RISER, mid)
		for sx in [-sw, sw]:
			b.quad(Vector3(sx, 0, zf), Vector3(sx, 0, zb), Vector3(sx, y1, zb), Vector3(sx, y1, zf), T_RISER.darkened(0.1), mid)
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(sx * 0.019, 0.0, z_front + 0.002), Vector3(sx * 0.019, 0.138, z_back - 0.002), 0.0048, T_WALL[1].darkened(0.1))
	# the shrine: red walls, a white portico with a dark door, a glazed roof in two pitches over a civ-coloured fascia, gold finial
	b.box(Vector3(0, 0.149, -0.004), Vector3(0.054, 0.034, 0.046), SHRINE_RED, SHRINE_RED)
	b.box(Vector3(0, 0.146, 0.0195), Vector3(0.02, 0.026, 0.003), DARK)
	b.box(Vector3(0, 0.134, 0.03), Vector3(0.05, 0.004, 0.02), LIME)
	for sx in [-1.0, 1.0]:
		b.prism(Vector3(sx * 0.02, 0.136, 0.031), 0.0042, 0.0036, 0.03, 6, LIME, LIME)
	b.prism(Vector3(0, 0.166, 0), 0.062, 0.046, 0.014, 4, GLAZE, GLAZE_DARK, PI * 0.25)
	b.prism(Vector3(0, 0.18, 0), 0.047, 0.0, 0.034, 4, GLAZE_HI, GLAZE_HI, PI * 0.25)
	# gilded hip ridges down both pitches
	var hips := [[0.166, 0.062, 0.18, 0.046], [0.18, 0.047, 0.214, 0.0]]
	for i in 4:
		var dir := Vector3(cos(PI * 0.25 + PI * 0.5 * i), 0, sin(PI * 0.25 + PI * 0.5 * i))
		var perp := Vector3(-dir.z, 0, dir.x) * 0.0016
		for hp in hips:
			var lo: Vector3 = dir * float(hp[1]) + Vector3(0, float(hp[0]), 0)
			var hi: Vector3 = dir * float(hp[3]) + Vector3(0, float(hp[2]), 0)
			var up := Vector3(0, 0.0022, 0)
			b.quad(lo + perp, hi + perp, hi + up, lo + up, GOLD, Vector3(0, 0.15, 0))
			b.quad(lo - perp, hi - perp, hi + up, lo + up, GOLD, Vector3(0, 0.15, 0))
	b.tag = M7MeshBuilder.TAG_CIV
	_band(b, 0.161, 0.0055, 0.0625, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.pole(Vector3(0, 0.205, 0), Vector3(0, 0.232, 0), 0.0028, GOLD)
	b.blob(Vector3(0, 0.222, 0), Vector3(0.0065, 0.0065, 0.0065), 5, 3, GOLD)
	# braziers on the second terrace, either side of the stair: a bronze bowl on a stone foot with a two-tone flame
	for sx in [-1.0, 1.0]:
		var p := Vector3(sx * 0.046, 0.092, 0.051)
		b.tag = M7MeshBuilder.TAG_PLAIN
		b.prism(p, 0.0065, 0.005, 0.01, 6, M7MeshKit.STONE_DARK, M7MeshKit.STONE_DARK)
		b.prism(p + Vector3(0, 0.01, 0), 0.006, 0.0115, 0.007, 6, BRONZE, DARK)
		b.tag = M7MeshBuilder.TAG_EMISSIVE
		b.prism(p + Vector3(0, 0.016, 0), 0.011, 0.0, 0.03, 6, FLAME, FLAME, 0.3)
		b.prism(p + Vector3(0, 0.017, 0), 0.006, 0.0, 0.022, 5, FLAME_CORE, FLAME_CORE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	# swallowtail pennants on all four buttresses, streaming outward
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			var x: float = sx * 0.078
			var z: float = sz * 0.078
			b.tag = M7MeshBuilder.TAG_PLAIN
			b.pole(Vector3(x, 0.054, z), Vector3(x, 0.19, z), 0.0026, M7MeshKit.WOOD)
			b.tag = M7MeshBuilder.TAG_CIV
			var y := 0.186
			var l := 0.04
			var p0 := Vector3(x, y, z)
			var p1 := Vector3(x + sx * l, y - 0.006, z)
			var p2 := Vector3(x + sx * (l - 0.012), y - 0.014, z)
			var p3 := Vector3(x + sx * l, y - 0.022, z)
			var p4 := Vector3(x, y - 0.028, z)
			var away := Vector3(x, y, z - sz * 0.3)
			b.tri(p0, p1, p2, Color.WHITE, away)
			b.tri(p0, p2, p4, Color.WHITE, away)
			b.tri(p2, p3, p4, Color.WHITE, away)
	kit.meshes["decor_temple"] = b.commit()

# ------------------------------------------------------------------ chief's hall (SOC-07)

static func _chief_hall(kit) -> void:
	# a long timber hall on a stone-kerbed platform: a coursed thatch roof with a civ ridge and crossed horn bargeboards, a
	# recessed porch of carved posts hung with shields, a firelit door, and two hanging standards flanking the step
	var b: M7MeshBuilder = kit._b(781)
	b.prism(Vector3.ZERO, 0.13, 0.118, 0.018, 8, KERB, YARD, PI / 8.0)
	b.box(Vector3(0, 0.0045, 0.124), Vector3(0.05, 0.009, 0.012), M7MeshKit.STONE, M7MeshKit.STONE.lightened(0.15))
	var wx := 0.095
	var wall_top := 0.068
	# body walls: dark planks, with lighter corner and wall posts
	b.box(Vector3(0, 0.018 + (wall_top - 0.018) * 0.5, -0.01), Vector3(wx * 2.0, wall_top - 0.018, 0.08), PLANK)
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * (wx - 0.004), 0.018 + (wall_top - 0.018) * 0.5, 0.051), Vector3(0.008, wall_top - 0.018, 0.042), PLANK.lightened(0.06))
	for x in [-0.088, -0.06, -0.032, 0.032, 0.06, 0.088]:
		b.box(Vector3(x, 0.044, 0.031), Vector3(0.006, 0.052, 0.003), M7MeshKit.WOOD_LIGHT)
	# the door: a light frame, the dark opening and the hearth's glow inside
	b.box(Vector3(0, 0.041, 0.0305), Vector3(0.042, 0.046, 0.002), M7MeshKit.WOOD_LIGHT)
	b.box(Vector3(0, 0.039, 0.0315), Vector3(0.03, 0.042, 0.002), DARK)
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	b.quad(Vector3(-0.011, 0.019, 0.033), Vector3(0.011, 0.019, 0.033), Vector3(0.008, 0.032, 0.033), Vector3(-0.008, 0.032, 0.033), GLOW, Vector3(0, 0.03, 0))
	b.tag = M7MeshBuilder.TAG_PLAIN
	# the roof: a flared profile (shallow at the eaves, steep above), thatch courses alternating in tone, a dark eave edge and
	# binding poles at the knee
	var zc := 0.01
	var rx := 0.113
	var prof := [Vector2(0.076, 0.064), Vector2(0.06, 0.08), Vector2(0.044, 0.095), Vector2(0.029, 0.123), Vector2(0.014, 0.151), Vector2(0.0, 0.178)]
	var ridge := 0.178
	for s in [-1.0, 1.0]:
		for k in prof.size() - 1:
			var p0: Vector2 = prof[k]
			var p1: Vector2 = prof[k + 1]
			var col: Color = M7MeshKit.THATCH if k % 2 == 0 else M7MeshKit.THATCH.darkened(0.12)
			if s < 0.0:
				col = col.darkened(0.05)
			# each course in three lengths of slightly different age
			for j in 3:
				var xa := -rx + rx * 2.0 * j / 3.0
				var xb := -rx + rx * 2.0 * (j + 1) / 3.0
				var cj := col.darkened(0.07 * b.rnd())
				b.quad(Vector3(xa, p0.y, zc + s * p0.x), Vector3(xb, p0.y, zc + s * p0.x), Vector3(xb, p1.y, zc + s * p1.x),
					Vector3(xa, p1.y, zc + s * p1.x), cj, Vector3(0, 0.0, zc))
		var pe: Vector2 = prof[0]
		b.quad(Vector3(-rx, pe.y, zc + s * pe.x), Vector3(rx, pe.y, zc + s * pe.x), Vector3(rx, pe.y - 0.008, zc + s * (pe.x - 0.002)),
			Vector3(-rx, pe.y - 0.008, zc + s * (pe.x - 0.002)), M7MeshKit.THATCH_DARK.darkened(0.25), Vector3(0, 0.1, zc))
		var pk: Vector2 = prof[2]
		b.pole(Vector3(-rx, pk.y + 0.002, zc + s * (pk.x + 0.001)), Vector3(rx, pk.y + 0.002, zc + s * (pk.x + 0.001)), 0.0028, M7MeshKit.WOOD)
	# gable ends of lighter boards, set in under the overhang
	for sx in [-1.0, 1.0]:
		var x: float = sx * wx
		for k in prof.size() - 1:
			var p0: Vector2 = prof[k]
			var p1: Vector2 = prof[k + 1]
			b.quad(Vector3(x, p0.y, zc + p0.x - 0.004), Vector3(x, p1.y, zc + p1.x - 0.004), Vector3(x, p1.y, zc - p1.x + 0.004),
				Vector3(x, p0.y, zc - p0.x + 0.004), M7MeshKit.WOOD_LIGHT, Vector3(0, 0.1, zc))
		b.box(Vector3(x, wall_top - 0.003, zc), Vector3(0.006, 0.006, 0.128), M7MeshKit.WOOD)
	# civ-coloured ridge: a broad cap down both slopes and a ridge pole
	b.tag = M7MeshBuilder.TAG_CIV
	var cap: Vector2 = prof[prof.size() - 2]
	for s in [-1.0, 1.0]:
		var lo := Vector3(0, ridge - (ridge - cap.y) * 0.45 + 0.0015, zc + s * cap.x * 0.45 + s * 0.0012)
		b.quad(Vector3(-rx - 0.002, lo.y, lo.z), Vector3(rx + 0.002, lo.y, lo.z), Vector3(rx + 0.002, ridge + 0.002, zc), Vector3(-rx - 0.002, ridge + 0.002, zc), Color.WHITE, Vector3(0, 0.0, zc))
	b.pole(Vector3(-rx - 0.004, ridge + 0.002, zc), Vector3(rx + 0.004, ridge + 0.002, zc), 0.0058, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	# bargeboards along the steep upper slope that cross over the ridge and end in pale horns
	for sx in [-1.0, 1.0]:
		var x: float = sx * (rx + 0.002)
		for s in [-1.0, 1.0]:
			var pb: Vector2 = prof[2]
			var e := Vector3(x, pb.y, zc + s * pb.x)
			var r := Vector3(x, ridge, zc)
			var tip := r + (r - e) * 0.28
			b.pole(e, tip, 0.0038, M7MeshKit.WOOD)
			b.pole(tip, tip + Vector3(sx * 0.007, 0.022, -s * 0.004), 0.0034, HORN)
	# smoke vents on the ridge
	for x in [-0.05, 0.05]:
		b.box(Vector3(x, ridge + 0.006, zc), Vector3(0.02, 0.01, 0.018), DARK, M7MeshKit.THATCH_DARK)
	# the porch: a beam on four carved posts, each with a civ band and a round civ shield with a dark rim and a bronze boss
	var pz := 0.074
	b.pole(Vector3(-wx, 0.069, pz), Vector3(wx, 0.069, pz), 0.004, M7MeshKit.WOOD)
	for x in [-0.07, -0.035, 0.035, 0.07]:
		b.pole(Vector3(x, 0.018, pz), Vector3(x, 0.072, pz), 0.0046, M7MeshKit.WOOD_LIGHT)
		b.tag = M7MeshBuilder.TAG_CIV
		b.prism(Vector3(x, 0.058, pz), 0.0056, 0.0056, 0.007, 6, Color.WHITE, Color.WHITE)
		b.tag = M7MeshBuilder.TAG_PLAIN
		var save := _with(b, Transform3D(Basis(Vector3.RIGHT, PI * 0.5), Vector3(x, 0.042, pz + 0.004)))
		b.prism(Vector3.ZERO, 0.0125, 0.0125, 0.002, 8, DARK, DARK)
		b.tag = M7MeshBuilder.TAG_CIV
		b.prism(Vector3(0, 0.002, 0), 0.0108, 0.0108, 0.0015, 8, Color.WHITE, Color.WHITE)
		b.tag = M7MeshBuilder.TAG_PLAIN
		b.prism(Vector3(0, 0.0035, 0), 0.0035, 0.0015, 0.002, 5, BRONZE, GOLD)
		b.xf = save
	# two standards behind the hall, their civ banners showing over the ridge: a tall pole, a cross bar, a banner with a gold
	# stripe and a notched foot
	for sx in [-1.0, 1.0]:
		var x: float = sx * 0.07
		var z := -0.08
		b.tag = M7MeshBuilder.TAG_PLAIN
		b.pole(Vector3(x, 0.018, z), Vector3(x, 0.272, z), 0.0034, M7MeshKit.WOOD)
		b.pole(Vector3(x - 0.02, 0.26, z), Vector3(x + 0.02, 0.26, z), 0.0024, M7MeshKit.WOOD)
		b.blob(Vector3(x, 0.275, z), Vector3(0.005, 0.006, 0.005), 5, 3, GOLD)
		b.tag = M7MeshBuilder.TAG_CIV
		var away := Vector3(x, 0.23, z - 0.3)
		var top := 0.259
		var bot := 0.204
		b.quad(Vector3(x - 0.018, top, z + 0.003), Vector3(x + 0.018, top, z + 0.003), Vector3(x + 0.018, bot, z + 0.003),
			Vector3(x - 0.018, bot, z + 0.003), Color.WHITE, away)
		b.tri(Vector3(x - 0.018, bot, z + 0.003), Vector3(x, bot, z + 0.003), Vector3(x - 0.018, bot - 0.014, z + 0.003), Color.WHITE, away)
		b.tri(Vector3(x + 0.018, bot, z + 0.003), Vector3(x, bot, z + 0.003), Vector3(x + 0.018, bot - 0.014, z + 0.003), Color.WHITE, away)
		b.tag = M7MeshBuilder.TAG_PLAIN
		b.quad(Vector3(x - 0.018, top - 0.012, z + 0.0035), Vector3(x + 0.018, top - 0.012, z + 0.0035), Vector3(x + 0.018, top - 0.017, z + 0.0035),
			Vector3(x - 0.018, top - 0.017, z + 0.0035), GOLD, away)
	# a pair of short carved posts with civ bands and pale horns flanking the step
	for sx in [-1.0, 1.0]:
		var x: float = sx * 0.04
		var z := 0.1
		b.prism(Vector3(x, 0.018, z), 0.0055, 0.0045, 0.03, 6, M7MeshKit.WOOD, M7MeshKit.WOOD)
		b.tag = M7MeshBuilder.TAG_CIV
		b.prism(Vector3(x, 0.032, z), 0.006, 0.006, 0.006, 6, Color.WHITE, Color.WHITE)
		b.tag = M7MeshBuilder.TAG_PLAIN
		for hx in [-1.0, 1.0]:
			b.pole(Vector3(x, 0.046, z), Vector3(x + hx * 0.009, 0.06, z), 0.0024, HORN)
	kit.meshes["decor_chiefhall"] = b.commit()

# ------------------------------------------------------------------ fleece bales (HUS-06)

static func _wool_bales(kit) -> void:
	# lumpy corded bales of fleece, a rack of drying fleeces in natural shades and one dyed in the civilization's colour, a
	# fleece spread on the ground and a basket of carded wool
	var b: M7MeshBuilder = kit._b(782)
	var bales := [[Vector3(-0.026, 0.0095, 0.0), 0.0, FLEECE], [Vector3(0.012, 0.0095, 0.004), 0.3, FLEECE_CREAM], [Vector3(-0.007, 0.0275, 0.002), 0.15, FLEECE]]
	for bale in bales:
		var bp: Vector3 = bale[0]
		var save := _with(b, Transform3D(Basis(Vector3.UP, float(bale[1])), Vector3.ZERO))
		var col: Color = bale[2]
		b.blob(bp, Vector3(0.018, 0.0105, 0.0125), 6, 3, col, 0.06, col.darkened(0.12))
		# two cords wrapped round it
		for dx in [-0.0065, 0.0065]:
			for i in 8:
				var a0 := TAU * i / 8.0
				var a1 := TAU * (i + 1) / 8.0
				var q0 := bp + Vector3(dx, sin(a0) * 0.0104, cos(a0) * 0.0124)
				var q1 := bp + Vector3(dx, sin(a1) * 0.0104, cos(a1) * 0.0124)
				var w := Vector3(0.0013, 0, 0)
				b.quad(q0 - w, q0 + w, q1 + w, q1 - w, CORD, bp + Vector3(dx, 0, 0))
		b.xf = save
	# the drying rack
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(sx * 0.032 + 0.02, 0, -0.034), Vector3(sx * 0.032 + 0.02, 0.052, -0.034), 0.003, M7MeshKit.WOOD)
	b.pole(Vector3(-0.016, 0.05, -0.034), Vector3(0.056, 0.05, -0.034), 0.0026, M7MeshKit.WOOD)
	var hung := [FLEECE, FLEECE_BROWN, Color.WHITE, FLEECE_DARK]
	for i in 4:
		b.tag = M7MeshBuilder.TAG_CIV if i == 2 else M7MeshBuilder.TAG_PLAIN
		b.blob(Vector3(-0.004 + i * 0.016, 0.034, -0.034), Vector3(0.0072, 0.0165, 0.0045), 5, 3, hung[i], 0.08)
	b.tag = M7MeshBuilder.TAG_PLAIN
	# a fleece spread out on the ground
	b.blob(Vector3(0.04, 0.0012, 0.024), Vector3(0.019, 0.0035, 0.013), 6, 3, FLEECE_CREAM, 0.15)
	# a basket of carded wool
	b.prism(Vector3(-0.05, 0, -0.022), 0.0095, 0.012, 0.014, 6, Color("#b08a4e"), Color("#7a5a2c"))
	b.blob(Vector3(-0.05, 0.015, -0.022), Vector3(0.0105, 0.0055, 0.0105), 5, 3, FLEECE, 0.12)
	kit.meshes["decor_woolbales"] = b.commit()

# ------------------------------------------------------------------ megalith pieces (STN-10, see monument_rig.gd)

## A closed rough column from rings [y, radius, colour of the band above it, lean x, lean z] and a point on top. Facets share
## corners, each varies a little in shade, and a ring whose colour is `null` gets a carved zigzag (alternating triangles) in
## ochre and chalk. Lower facets sometimes carry lichen.
static func _rough_column(b: M7MeshBuilder, rings: Array, sides: int, tip: Vector3, tip_col: Color) -> void:
	var pts := []
	for ring in rings:
		var row := []
		for i in sides:
			var a := TAU * (i + 0.3 * b.rnd()) / sides
			var j := 1.0 + 0.06 * b.rnds()
			row.append(Vector3(cos(a) * float(ring[1]) * j + float(ring[3]), float(ring[0]), sin(a) * float(ring[1]) * j + float(ring[4])))
		pts.append(row)
	for k in rings.size():
		var last := k == rings.size() - 1
		var y_mid := float(rings[k][0])
		var axis := Vector3(float(rings[k][3]), y_mid, float(rings[k][4]))
		for i in sides:
			var i1 := (i + 1) % sides
			var a: Vector3 = pts[k][i]
			var c: Vector3 = pts[k][i1]
			if last:
				b.tri(a, c, tip, tip_col.darkened(0.06 * b.rnd()), axis)
				continue
			var d: Vector3 = pts[k + 1][i1]
			var e: Vector3 = pts[k + 1][i]
			if typeof(rings[k][2]) == TYPE_NIL:
				b.tri(a, c, d, OCHRE, axis)
				b.tri(a, d, e, CHALK, axis)
				continue
			var col: Color = rings[k][2]
			if k <= 1 and b.rnd() < 0.25:
				col = LICHEN
			col = col.darkened(0.09 * b.rnd())
			b.quad(a, c, d, e, col, axis)

static func _megalith(kit) -> void:
	var rock: Color = M7MeshKit.ROCK
	var rock_hi: Color = M7MeshKit.ROCK_HI
	# standing stone, foot at the origin, 0.17 tall: a weathered, faceted, slightly leaning pillar with a dark foot, an ochre band
	# and a carved zigzag above it
	var b: M7MeshBuilder = kit._b(730)
	var rings := [
		[0.0, 0.034, STONE_FOOT, 0.0, 0.0], [0.028, 0.0345, rock, 0.0005, 0.0], [0.064, 0.033, OCHRE, 0.001, 0.0005],
		[0.076, 0.0325, rock, 0.0012, 0.0006], [0.1, 0.0305, null, 0.0016, 0.0008], [0.111, 0.0295, rock_hi, 0.0018, 0.001],
		[0.14, 0.0255, rock_hi, 0.0022, 0.0012], [0.16, 0.0175, rock_hi.lightened(0.05), 0.0026, 0.0014]]
	_rough_column(b, rings, 7, Vector3(0.003, 0.17, 0.0015), rock_hi.lightened(0.08))
	kit.meshes["monument_menhir"] = b.commit()

	# lintel, centred, long axis along X (0.26 x 0.04 x 0.06): a dressed slab with chamfered top edges, a weathered top, darker
	# ends and an ochre-painted band across its middle
	b = kit._b(731)
	var hy := 0.02
	var hz := 0.03
	var ch := 0.007
	var prof := [Vector2(-hy, -hz), Vector2(-hy, hz), Vector2(hy - ch, hz), Vector2(hy, hz - ch), Vector2(hy, -hz + ch), Vector2(hy - ch, -hz)]
	var xs := [-0.13, -0.021, 0.021, 0.13]
	var secs := []
	for si in xs.size():
		var sec := []
		var inner: bool = si > 0 and si < xs.size() - 1
		for q in prof.size():
			var pp: Vector2 = prof[q]
			var y := pp.x
			var z := pp.y
			if inner and y > 0.0:
				y -= 0.003 * b.rnd()
				z *= 1.0 - 0.06 * b.rnd()
			sec.append(Vector3(float(xs[si]), y, z))
		secs.append(sec)
	# bottom, +Z side, chamfer, top, chamfer, -Z side
	var lintel_cols := [rock.darkened(0.18), rock, rock_hi, rock_hi.lightened(0.06), rock_hi, rock]
	for si in xs.size() - 1:
		for e in prof.size():
			var e1 := (e + 1) % prof.size()
			var col: Color = lintel_cols[e]
			if si == 1 and e != 3:
				col = OCHRE.lightened(0.1) if e == 2 or e == 4 else OCHRE
			else:
				col = col.darkened(0.05 * b.rnd())
			b.quad(secs[si][e], secs[si][e1], secs[si + 1][e1], secs[si + 1][e], col, Vector3((float(xs[si]) + float(xs[si + 1])) * 0.5, 0, 0))
	for si in [0, xs.size() - 1]:
		var sec: Array = secs[si]
		for t in range(1, prof.size() - 1):
			b.tri(sec[0], sec[t], sec[t + 1], rock.darkened(0.1), Vector3.ZERO)
	kit.meshes["monument_lintel"] = b.commit()

	# drag sledge, centred, long axis along Z: squared runners with an upturned bow at +Z, cross boards in two tones lashed at the
	# ends, and a tow bar with rope loops at -Z where the hauling ropes are tied
	b = kit._b(732)
	for sx in [-0.03, 0.03]:
		b.pole(Vector3(sx, 0.006, -0.1), Vector3(sx, 0.006, 0.09), 0.0045, M7MeshKit.WOOD)
		b.pole(Vector3(sx, 0.006, 0.09), Vector3(sx, 0.02, 0.112), 0.0045, M7MeshKit.WOOD)
	var zs := [-0.08, -0.03, 0.02, 0.07]
	for i in zs.size():
		var z: float = zs[i]
		var wood: Color = M7MeshKit.WOOD_LIGHT if i % 2 == 0 else M7MeshKit.WOOD_LIGHT.lightened(0.14)
		b.box(Vector3(0, 0.014, z), Vector3(0.08, 0.008, 0.014), wood)
		if i == 0 or i == zs.size() - 1:
			for sx in [-0.03, 0.03]:
				b.box(Vector3(sx, 0.0125, z), Vector3(0.0075, 0.0125, 0.0165), ROPE.darkened(0.1), ROPE)
	b.pole(Vector3(-0.036, 0.01, -0.101), Vector3(0.036, 0.01, -0.101), 0.0034, M7MeshKit.WOOD)
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(sx * 0.03, 0.01, -0.101), Vector3(sx * 0.05, 0.027, -0.104), 0.0022, ROPE)
	kit.meshes["monument_sledge"] = b.commit()

	# roller log, axis along X: bark sides in two tones and pale sawn ends
	b = kit._b(733)
	var sides := 7
	var rr := 0.0075
	for i in sides:
		var a0 := TAU * i / sides
		var a1 := TAU * (i + 1) / sides
		var p0 := Vector3(0, cos(a0) * rr, sin(a0) * rr)
		var p1 := Vector3(0, cos(a1) * rr, sin(a1) * rr)
		var bark: Color = M7MeshKit.BARK if i % 2 == 0 else M7MeshKit.BARK.lightened(0.12)
		b.quad(p0 + Vector3(-0.045, 0, 0), p1 + Vector3(-0.045, 0, 0), p1 + Vector3(0.045, 0, 0), p0 + Vector3(0.045, 0, 0), bark, Vector3.ZERO)
		for sx in [-0.045, 0.045]:
			b.tri(Vector3(sx, 0, 0), p0 + Vector3(sx, 0, 0), p1 + Vector3(sx, 0, 0), CUT_WOOD if i % 3 != 0 else CUT_WOOD.darkened(0.1), Vector3.ZERO)
	kit.meshes["monument_roller"] = b.commit()

	# drag rope, one unit long along +Z from the origin (the rig stretches it): a slight sag and a twist of two tones
	b = kit._b(734)
	var hs := 0.002
	var pts := [Vector3(0, 0, 0), Vector3(0, -0.005, 0.333), Vector3(0, -0.005, 0.667), Vector3(0, 0, 1.0)]
	for s in 3:
		var p0: Vector3 = pts[s]
		var p1: Vector3 = pts[s + 1]
		var corners := [Vector3(-hs, -hs, 0), Vector3(hs, -hs, 0), Vector3(hs, hs, 0), Vector3(-hs, hs, 0)]
		for k in 4:
			var c0: Vector3 = corners[k]
			var c1: Vector3 = corners[(k + 1) % 4]
			var col := ROPE if (k + s) % 2 == 0 else ROPE.darkened(0.18)
			b.quad(p0 + c0, p0 + c1, p1 + c1, p1 + c0, col, (p0 + p1) * 0.5)
	kit.meshes["monument_rope"] = b.commit()
