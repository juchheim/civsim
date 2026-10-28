## Village decor props
##
## Art pass: meshes in this file REPLACE the ones mesh_kit.gd/looks_kit.gd build under the same names (`kit.meshes[name] = ...`), so
## nothing that places them changes. Keep: the mesh name, the origin (ground centre), the facing (doors/fronts toward +Z), the footprint
## (the village layout spaces dwellings by DWELL_RADIUS in world_builder.gd), and the tag meanings (TAG_CIV faces take the owner's
## colour, TAG_EMISSIVE glows). `kit` is the M7MeshKit: use kit._b(seed) for a builder and kit.WOOD, kit.THATCH ... for its palette.
##
## Each prop is drawn small (village scale 1.45, about 0.17 apart), so each is built around one bold silhouette and colour that says
## what the people learned (a stone man, a sun disc, a horned shrine, a clay shaft furnace), with a few small details for close views.
extends RefCounted

const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const BARK := Color("#4b3a2a")
const HIDE := Color("#b99a72")
const HIDE_DARK := Color("#8f7354")
const STONE := Color("#8f8a80")
const STONE_DARK := Color("#6c675f")
const ROCK := Color("#8e877c")
const ROCK_HI := Color("#a39c90")
const PALE_STONE := Color("#c4beb2")
const CREAM := Color("#f3ead6")
const SOOT := Color("#2b221c")
const OCHRE := Color("#a8432a")
const GOLD := Color("#e0ac3a")
const GOLD_DARK := Color("#a87a24")
const LIME := Color("#efe4c8")
const CLAY := Color("#b5603c")
const CLAY_DARK := Color("#7f4329")
const FRESH := Color("#d4b07c")        # freshly cut wood
const CUT_STONE := Color("#d6cdb8")    # freshly quarried faces
const STRAW := Color("#d1aa62")
const STRAW_DARK := Color("#a07a3c")
const EMBER := Color("#ff6a20")
const EMBER_HI := Color("#ffb347")

const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const GLOW := M7MeshBuilder.TAG_EMISSIVE

static func build(kit) -> void:
	_belief(kit)
	_craft(kit)
	_works(kit)
	_camp(kit)

# ------------------------------------------------------------------ helpers

## Moves the next parts to p, turned by yaw (about Y), tilted by tilt (about X: + leans the top toward +Z) and rolled (about Z).
static func _at(b: M7MeshBuilder, p: Vector3, yaw: float = 0.0, tilt: float = 0.0, roll: float = 0.0) -> void:
	b.xf = Transform3D(Basis.from_euler(Vector3(tilt, yaw, roll)), p)

static func _home(b: M7MeshBuilder) -> void:
	b.xf = Transform3D.IDENTITY

## A turned vessel: rings of [radius, height] from the bottom up, band k coloured cols[k]; `mouth` fills the top opening.
static func _lathe(b: M7MeshBuilder, p: Vector3, prof: Array, sides: int, cols: Array, mouth = null, sc: float = 1.0) -> void:
	var rings := []
	for ring_def in prof:
		var r: float = ring_def[0] * sc
		var y: float = ring_def[1] * sc
		var ring := []
		for s in sides:
			var a := TAU * s / sides
			ring.append(p + Vector3(cos(a) * r, y, sin(a) * r))
		rings.append(ring)
	for k in prof.size() - 1:
		var hint := p + Vector3(0, (float(prof[k][1]) + float(prof[k + 1][1])) * 0.5 * sc, 0)
		for s in sides:
			var s1 := (s + 1) % sides
			b.quad(rings[k][s], rings[k][s1], rings[k + 1][s1], rings[k + 1][s], cols[k % cols.size()], hint)
	if mouth != null:
		var top: Array = rings[rings.size() - 1]
		var cy: float = prof[prof.size() - 1][1] * sc
		for s in sides:
			b.tri(p + Vector3(0, cy - 0.003, 0), top[s], top[(s + 1) % sides], mouth, p)

## A flat cord or thread between two points, w wide across `side`.
static func _cord(b: M7MeshBuilder, a: Vector3, c: Vector3, w: float, col: Color, side: Vector3) -> void:
	var o := side.normalized() * w * 0.5
	b.quad(a - o, a + o, c + o, c - o, col)

# ------------------------------------------------------------------ belief: totem, cairn, shrine, grave, council, sky pole

static func _belief(kit) -> void:
	# totem: stacked carved faces, a civ-painted band and a black thunderbird with red-tipped wings spread on top
	var b: M7MeshBuilder = kit._b(700)
	b.prism(Vector3.ZERO, 0.026, 0.02, 0.01, 5, STONE_DARK, STONE)
	b.box(Vector3(0, 0.033, 0), Vector3(0.032, 0.046, 0.032), Color("#a4472c"))
	b.box(Vector3(0, 0.042, 0.0165), Vector3(0.026, 0.007, 0.003), SOOT)
	b.box(Vector3(0, 0.022, 0.0165), Vector3(0.016, 0.006, 0.003), CREAM)
	b.tag = CIV
	b.box(Vector3(0, 0.062, 0), Vector3(0.037, 0.012, 0.037), Color.WHITE)
	b.tag = PLAIN
	b.box(Vector3(0, 0.085, 0), Vector3(0.028, 0.034, 0.028), Color("#d8c08a"))
	b.box(Vector3(0, 0.091, 0.0145), Vector3(0.022, 0.006, 0.003), SOOT)
	b.box(Vector3(0, 0.08, 0.016), Vector3(0.007, 0.012, 0.007), Color("#a4472c"))
	for side in [-1.0, 1.0]:
		_at(b, Vector3(side * 0.031, 0.112, 0), 0.0, 0.0, side * 0.38)
		b.box(Vector3.ZERO, Vector3(0.048, 0.014, 0.008), SOOT)
		b.box(Vector3(side * 0.022, 0, 0), Vector3(0.012, 0.017, 0.01), OCHRE)
	_home(b)
	b.box(Vector3(0, 0.116, 0.002), Vector3(0.02, 0.026, 0.022), SOOT)
	b.box(Vector3(0, 0.112, 0.018), Vector3(0.008, 0.008, 0.014), GOLD)
	b.box(Vector3(0, 0.122, 0.0125), Vector3(0.016, 0.004, 0.002), CREAM)
	kit.meshes["decor_totem"] = b.commit()

	# cairn: a stone man (inukshuk) on a rubble foot, arms out to show the way, a civ-dyed rag tied to one arm
	b = kit._b(701)
	b.blob(Vector3.ZERO, Vector3(0.042, 0.012, 0.032), 6, 3, STONE_DARK, 0.18)
	_at(b, Vector3(-0.016, 0.025, 0), 0.15, 0.0, 0.06)
	b.box(Vector3.ZERO, Vector3(0.016, 0.032, 0.018), STONE, ROCK_HI)
	_at(b, Vector3(0.017, 0.025, 0.001), -0.2, 0.0, -0.05)
	b.box(Vector3.ZERO, Vector3(0.017, 0.032, 0.017), ROCK, ROCK_HI)
	_at(b, Vector3(0, 0.046, 0), 0.06, 0.0, 0.03)
	b.box(Vector3.ZERO, Vector3(0.058, 0.011, 0.024), ROCK_HI, PALE_STONE)
	_at(b, Vector3(0.002, 0.063, 0), -0.12)
	b.box(Vector3.ZERO, Vector3(0.026, 0.024, 0.02), STONE, ROCK_HI)
	_at(b, Vector3(0, 0.08, 0), 0.07, 0.0, -0.04)
	b.box(Vector3.ZERO, Vector3(0.09, 0.01, 0.018), ROCK_HI, PALE_STONE)
	_at(b, Vector3(-0.001, 0.095, 0), 0.4, 0.0, 0.08)
	b.box(Vector3.ZERO, Vector3(0.019, 0.02, 0.017), STONE, ROCK_HI)
	_home(b)
	b.tag = CIV
	b.quad(Vector3(0.034, 0.077, 0.0095), Vector3(0.042, 0.076, 0.0095), Vector3(0.045, 0.054, 0.012), Vector3(0.037, 0.056, 0.012), Color.WHITE)
	kit.meshes["decor_cairn"] = b.commit()

	# shrine: a whitewashed niche on a stone plinth, a red roof crowned with horns of consecration, a lamp lit at the idol's feet
	b = kit._b(706)
	b.prism(Vector3.ZERO, 0.056, 0.052, 0.01, 6, STONE_DARK, STONE, PI / 6.0)
	b.box(Vector3(0, 0.042, -0.016), Vector3(0.05, 0.064, 0.008), Color("#6a3a26"))
	b.box(Vector3(-0.022, 0.042, 0.003), Vector3(0.008, 0.064, 0.034), LIME)
	b.box(Vector3(0.022, 0.042, 0.003), Vector3(0.008, 0.064, 0.034), LIME)
	b.box(Vector3(0, 0.078, 0), Vector3(0.062, 0.008, 0.044), OCHRE, LIME)
	b.box(Vector3(0, 0.086, 0), Vector3(0.04, 0.008, 0.014), LIME)
	for side in [-1.0, 1.0]:
		b.pole(Vector3(side * 0.014, 0.088, 0), Vector3(side * 0.023, 0.104, 0), 0.0045, LIME)
		b.pole(Vector3(side * 0.023, 0.104, 0), Vector3(side * 0.018, 0.118, 0), 0.0035, LIME)
	b.prism(Vector3(0, 0.01, -0.007), 0.0075, 0.003, 0.026, 5, OCHRE, OCHRE)
	b.blob(Vector3(0, 0.04, -0.007), Vector3(0.005, 0.006, 0.005), 5, 2, OCHRE)
	b.tag = GLOW
	b.box(Vector3(0, 0.015, 0.01), Vector3(0.008, 0.008, 0.008), EMBER_HI)
	kit.meshes["decor_shrine"] = b.commit()

	# grave: a long earth mound kerbed with pale stones, an upright headstone at its head, red ochre and flowers strewn on top
	b = kit._b(714)
	b.blob(Vector3(0, 0, 0.006), Vector3(0.03, 0.017, 0.046), 7, 3, Color("#6f5440"), 0.06)
	b.box(Vector3(0, 0.013, 0.008), Vector3(0.014, 0.006, 0.044), OCHRE)
	var kerb := 10
	for k in kerb:
		var a0 := TAU * k / kerb
		var a1 := TAU * (k + 1) / kerb
		var col := PALE_STONE if k % 2 == 0 else ROCK_HI
		var o0 := Vector3(cos(a0) * 0.04, 0, 0.006 + sin(a0) * 0.058)
		var o1 := Vector3(cos(a1) * 0.04, 0, 0.006 + sin(a1) * 0.058)
		var i0 := Vector3(cos(a0) * 0.029, 0.0, 0.006 + sin(a0) * 0.045)
		var i1 := Vector3(cos(a1) * 0.029, 0.0, 0.006 + sin(a1) * 0.045)
		b.quad(o0, o1, o1 * 0.97 + Vector3(0, 0.008, 0), o0 * 0.97 + Vector3(0, 0.008, 0), col, Vector3(0, 0.004, 0.006))
		b.quad(o0 * 0.97 + Vector3(0, 0.008, 0), o1 * 0.97 + Vector3(0, 0.008, 0), i1, i0, col.lightened(0.08), Vector3(0, -0.02, 0.006))
	_at(b, Vector3(0, 0.032, -0.054), 0.08, -0.08)
	b.box(Vector3.ZERO, Vector3(0.032, 0.064, 0.011), ROCK_HI, PALE_STONE)
	b.quad(Vector3(-0.006, 0.006, 0.0058), Vector3(0.006, 0.006, 0.0058), Vector3(0.006, 0.018, 0.0058), Vector3(-0.006, 0.018, 0.0058), OCHRE)
	_home(b)
	b.box(Vector3(-0.007, 0.017, 0.022), Vector3(0.006, 0.004, 0.006), Color("#f2d24a"))
	b.box(Vector3(0.006, 0.016, -0.014), Vector3(0.006, 0.004, 0.006), CREAM)
	kit.meshes["decor_grave"] = b.commit()

	# council circle: log benches round a small council fire, open to the front, the chief's staff flying the civ banner behind
	b = kit._b(715)
	for k in 5:
		var a := PI * 0.5 + 0.75 + k * (TAU - 1.5) / 4.0
		var p := Vector3(cos(a) * 0.052, 0.0075, sin(a) * 0.052)
		var t := Vector3(-sin(a), 0, cos(a))
		b.pole(p - t * 0.024, p + t * 0.024, 0.0075, FRESH if k % 2 == 0 else WOOD_LIGHT, 5)
	for k in 5:
		var a := TAU * k / 5.0
		b.prism(Vector3(cos(a) * 0.015, 0, sin(a) * 0.015), 0.006, 0.0, 0.007, 3, STONE, STONE, a)
	b.pole(Vector3(0, 0, -0.066), Vector3(0, 0.135, -0.066), 0.0032, WOOD)
	b.pole(Vector3(-0.002, 0.128, -0.066), Vector3(0.036, 0.128, -0.066), 0.0018, WOOD, 3)
	b.box(Vector3(0, 0.138, -0.066), Vector3(0.009, 0.009, 0.009), GOLD)
	b.tag = CIV
	b.box(Vector3(0.018, 0.111, -0.066), Vector3(0.032, 0.032, 0.002), Color.WHITE)
	b.tag = GLOW
	b.prism(Vector3.ZERO, 0.011, 0.007, 0.006, 5, EMBER, EMBER_HI)
	kit.meshes["decor_council"] = b.commit()

	# sky pole: a tall notched gnomon crowned with a rayed gold sun, in a ring of stones with tall markers at the quarters
	b = kit._b(717)
	b.pole(Vector3.ZERO, Vector3(0, 0.15, 0), 0.0045, FRESH)
	for y in [0.05, 0.08, 0.11]:
		b.box(Vector3(0, y, 0), Vector3(0.011, 0.007, 0.011), OCHRE)
	for k in 8:
		var a := TAU * k / 8.0
		var p := Vector3(cos(a) * 0.058, 0, sin(a) * 0.058)
		if k % 2 == 0:
			b.prism(p, 0.0095, 0.0065, 0.034, 4, ROCK_HI, PALE_STONE, PI / 4.0 + a)
		else:
			b.prism(p, 0.0085, 0.0, 0.013, 4, STONE, STONE, a)
	var sun := Vector3(0, 0.166, 0)
	b.blob(sun, Vector3(0.019, 0.019, 0.019), 6, 3, GOLD, 0.0, GOLD_DARK)
	for k in 8:
		var a := TAU * k / 8.0 + PI / 8.0
		var d := Vector3(cos(a), sin(a), 0)
		var e := Vector3(0, sin(a), cos(a))
		var dw := Vector3(-sin(a), cos(a), 0) * 0.006
		var ew := Vector3(0, cos(a), -sin(a)) * 0.006
		b.tri(sun + d * 0.014 + dw, sun + d * 0.014 - dw, sun + d * 0.039, GOLD)
		b.tri(sun + e * 0.014 + ew, sun + e * 0.014 - ew, sun + e * 0.039, GOLD)
	kit.meshes["decor_skypole"] = b.commit()

# ------------------------------------------------------------------ crafts: hide frame, pots, loom, baskets, woodwork, bricks

static func _craft(kit) -> void:
	# hide frame: a pale scraped pelt laced into a leaning pole frame, propped from behind, a hair-on pelt waiting on the ground
	var b: M7MeshBuilder = kit._b(703)
	_at(b, Vector3(0, 0, -0.004), 0.0, -0.2)
	b.pole(Vector3(-0.05, 0, 0), Vector3(-0.05, 0.13, 0), 0.004, WOOD)
	b.pole(Vector3(0.05, 0, 0), Vector3(0.05, 0.13, 0), 0.004, WOOD)
	b.pole(Vector3(-0.062, 0.114, 0), Vector3(0.062, 0.114, 0), 0.0035, WOOD)
	b.pole(Vector3(-0.062, 0.018, 0), Vector3(0.062, 0.018, 0), 0.0035, WOOD)
	var pelt := [Vector2(0, 0.04), Vector2(0.012, 0.03), Vector2(0.034, 0.041), Vector2(0.03, 0.022), Vector2(0.026, 0.0),
		Vector2(0.031, -0.024), Vector2(0.038, -0.038), Vector2(0.014, -0.03), Vector2(0, -0.038)]
	for k in range(7, 0, -1):
		pelt.append(Vector2(-pelt[k].x, pelt[k].y))
	var pc := Vector3(0, 0.066, 0.001)
	for k in pelt.size():
		var p0: Vector2 = pelt[k]
		var p1: Vector2 = pelt[(k + 1) % pelt.size()]
		b.tri(pc, pc + Vector3(p0.x, p0.y, 0), pc + Vector3(p1.x, p1.y, 0), Color("#e2cb9c") if k % 2 == 0 else Color("#d3b888"), pc - Vector3(0, 0, 1))
	for cd in [[Vector2(0.034, 0.107), Vector2(0.05, 0.113)], [Vector2(0.026, 0.066), Vector2(0.05, 0.066)],
			[Vector2(0.038, 0.028), Vector2(0.05, 0.019)], [Vector2(0.0, 0.106), Vector2(0.0, 0.114)], [Vector2(0.0, 0.028), Vector2(0.0, 0.018)]]:
		for side in ([1.0] if cd[0].x == 0.0 else [-1.0, 1.0]):
			var a := Vector3(cd[0].x * side, cd[0].y, 0.0015)
			var c := Vector3(cd[1].x * side, cd[1].y, 0.0015)
			var d := c - a
			_cord(b, a, c, 0.0018, SOOT, Vector3(-d.y, d.x, 0))
	_home(b)
	for side in [-1.0, 1.0]:
		b.pole(Vector3(side * 0.05, 0.115, -0.027), Vector3(side * 0.042, 0, -0.072), 0.003, BARK)
	var fc := Vector3(0.036, 0.0025, 0.044)
	for k in pelt.size():
		var p0: Vector2 = pelt[k] * 0.62
		var p1: Vector2 = pelt[(k + 1) % pelt.size()] * 0.62
		b.tri(fc, fc + Vector3(p0.x, -0.0015, p0.y), fc + Vector3(p1.x, -0.0015, p1.y), Color("#6e4f35") if k % 2 == 0 else Color("#5e432d"), fc - Vector3(0, 1, 0))
	kit.meshes["decor_hideframe"] = b.commit()

	# pots: a tall cream-banded storage jar, a black burnished cooking pot with a red band and an open bowl
	b = kit._b(704)
	_lathe(b, Vector3(-0.012, 0, -0.016), [[0.011, 0.0], [0.022, 0.016], [0.023, 0.03], [0.012, 0.05], [0.0095, 0.058], [0.014, 0.064]], 7,
			[CLAY.darkened(0.12), CLAY, Color("#ead9b4"), CLAY, CLAY.lightened(0.08)], SOOT, 1.25)
	_lathe(b, Vector3(0.032, 0, 0.012), [[0.008, 0.0], [0.017, 0.01], [0.016, 0.022], [0.01, 0.028], [0.012, 0.031]], 7,
			[Color("#3a2c25"), Color("#3a2c25"), OCHRE, Color("#3a2c25")], SOOT, 1.25)
	_lathe(b, Vector3(-0.03, 0, 0.032), [[0.006, 0.0], [0.014, 0.006], [0.018, 0.012]], 7,
			[CLAY.lightened(0.1), CLAY.lightened(0.18)], CLAY_DARK, 1.2)
	kit.meshes["decor_pots"] = b.commit()

	# loom: a leaning warp-weighted loom, the woven cloth in red, cream and indigo bands with a zigzag, weights and yarn at its foot
	b = kit._b(705)
	var loom_red := Color("#b23a2a")
	var indigo := Color("#34507e")
	var linen := Color("#ece0c2")
	_at(b, Vector3(0, 0, -0.012), 0.0, -0.17)
	b.pole(Vector3(-0.048, 0, 0), Vector3(-0.048, 0.14, 0), 0.0042, WOOD)
	b.pole(Vector3(0.048, 0, 0), Vector3(0.048, 0.14, 0), 0.0042, WOOD)
	b.pole(Vector3(-0.058, 0.124, 0.002), Vector3(0.058, 0.124, 0.002), 0.005, WOOD_LIGHT)
	for band in [[0.12, 0.108, linen], [0.108, 0.098, loom_red], [0.098, 0.084, linen], [0.084, 0.075, indigo], [0.075, 0.068, linen]]:
		var y0: float = band[0]
		var y1: float = band[1]
		b.quad(Vector3(-0.04, y0, 0.003), Vector3(0.04, y0, 0.003), Vector3(0.04, y1, 0.003), Vector3(-0.04, y1, 0.003), band[2], Vector3(0, y0, -1))
	for i in 8:
		var x0 := -0.04 + i * 0.01
		b.tri(Vector3(x0, 0.085, 0.0036), Vector3(x0 + 0.01, 0.085, 0.0036), Vector3(x0 + 0.005, 0.097, 0.0036), loom_red, Vector3(0, 0.09, -1))
	for i in 9:
		var x := -0.036 + i * 0.009
		var wx := -0.036 + (i >> 1) * 0.018
		_cord(b, Vector3(x, 0.068, 0.0026), Vector3(wx, 0.026, 0.0026), 0.0016, Color("#d9cba8"), Vector3.RIGHT)
	b.pole(Vector3(-0.054, 0.05, 0.005), Vector3(0.054, 0.05, 0.005), 0.0026, WOOD_LIGHT)
	for i in 5:
		b.prism(Vector3(-0.036 + i * 0.018, 0.012, 0.0026), 0.0058, 0.0, 0.015, 4, CLAY_DARK, CLAY_DARK, PI / 4.0)
	_home(b)
	b.blob(Vector3(0.03, 0.008, 0.04), Vector3(0.009, 0.008, 0.009), 5, 3, loom_red)
	b.blob(Vector3(0.047, 0.007, 0.027), Vector3(0.0075, 0.007, 0.0075), 5, 3, indigo)
	kit.meshes["decor_loom"] = b.commit()

	# baskets: a tall conical carrying basket of nuts and round baskets of yellow grain and red berries, woven in light and dark bands
	b = kit._b(719)
	_lathe(b, Vector3(-0.014, 0, -0.016), [[0.011, 0.0], [0.016, 0.016], [0.021, 0.034], [0.026, 0.056]], 7, [STRAW_DARK, STRAW, STRAW_DARK])
	b.blob(Vector3(-0.014, 0.053, -0.016), Vector3(0.023, 0.008, 0.023), 6, 2, Color("#6b4426"), 0.15)
	for bk in [[Vector3(0.026, 0, 0.012), 0.019, Color("#e8c04a")], [Vector3(-0.026, 0, 0.032), 0.016, Color("#b8323a")]]:
		var p: Vector3 = bk[0]
		var r: float = bk[1]
		_lathe(b, p, [[r * 0.65, 0.0], [r * 0.95, r * 0.55], [r, r * 0.95]], 7, [STRAW, STRAW_DARK])
		b.blob(p + Vector3(0, r * 0.9, 0), Vector3(r * 0.92, r * 0.38, r * 0.92), 6, 2, bk[2], 0.12)
	kit.meshes["decor_baskets"] = b.commit()

	# woodwork: a chopping stump with an axe bitten into it, a log on a trestle, freshly sawn planks and a scatter of chips
	b = kit._b(720)
	b.prism(Vector3(-0.032, 0, 0.018), 0.018, 0.016, 0.026, 7, BARK, FRESH)
	b.pole(Vector3(-0.028, 0.03, 0.02), Vector3(-0.006, 0.058, 0.03), 0.0026, WOOD_LIGHT)
	_at(b, Vector3(-0.031, 0.028, 0.019), 0.4, 0.0, 0.7)
	b.box(Vector3.ZERO, Vector3(0.016, 0.006, 0.006), STONE_DARK)
	_home(b)
	for x in [0.012, 0.048]:
		b.pole(Vector3(x, 0, -0.04), Vector3(x, 0.046, -0.006), 0.0028, WOOD)
		b.pole(Vector3(x, 0, -0.006), Vector3(x, 0.046, -0.04), 0.0028, WOOD)
	b.pole(Vector3(-0.004, 0.044, -0.023), Vector3(0.066, 0.044, -0.023), 0.009, BARK, 6)
	_at(b, Vector3(0.066, 0.044, -0.023), 0.0, 0.0, -PI * 0.5)
	b.prism(Vector3.ZERO, 0.0085, 0.0085, 0.0012, 6, FRESH, FRESH.lightened(0.1))
	_home(b)
	for k in 3:
		b.box(Vector3(0.03 - k * 0.002, 0.003 + k * 0.006, 0.038 + k * 0.002), Vector3(0.05, 0.005, 0.013), FRESH.darkened(0.06 * k), FRESH.lightened(0.08))
	for ch in [Vector3(-0.006, 0, 0.034), Vector3(-0.05, 0, 0.042), Vector3(-0.054, 0, -0.004), Vector3(-0.014, 0, -0.01)]:
		b.tri(ch + Vector3(-0.005, 0.001, 0), ch + Vector3(0.004, 0.001, -0.003), ch + Vector3(0.001, 0.001, 0.005), FRESH.lightened(0.15), ch)
	kit.meshes["decor_woodwork"] = b.commit()

	# mudbricks: dark wet bricks laid in rows to dry before a cross-stacked pile of sun-dried ones, a heap of mud and the mould
	b = kit._b(718)
	var wet := Color("#7e5434")
	var dry := Color("#c48d5a")
	for row in 2:
		for i in 4:
			b.box(Vector3(-0.038 + i * 0.022 + row * 0.006, 0.0035, 0.028 + row * 0.02), Vector3(0.018, 0.007, 0.011), wet, wet.lightened(0.08))
	for layer in 4:
		var y := 0.005 + layer * 0.0105
		var n: int = [4, 3, 3, 2][layer]
		for k in n:
			var off := (k - (n - 1) * 0.5) * 0.0135
			var jit := b.rnds() * 0.0018
			var col: Color = [dry, dry.lightened(0.12), dry.darkened(0.1)][(layer * 2 + k) % 3]
			if layer % 2 == 0:
				b.box(Vector3(0.012 + jit, y, -0.028 + off), Vector3(0.034, 0.0095, 0.0105), col, col.lightened(0.1))
			else:
				b.box(Vector3(0.012 + off, y, -0.028 + jit), Vector3(0.0105, 0.0095, 0.034), col, col.lightened(0.1))
	b.blob(Vector3(-0.042, 0, -0.026), Vector3(0.018, 0.013, 0.017), 6, 2, wet.darkened(0.15), 0.12)
	b.tri(Vector3(-0.048, 0.012, -0.022), Vector3(-0.036, 0.013, -0.026), Vector3(-0.042, 0.012, -0.016), STRAW, Vector3(-0.042, 0, -0.026))
	for side in [-1.0, 1.0]:
		b.box(Vector3(-0.042 + side * 0.012, 0.003, 0.004), Vector3(0.004, 0.006, 0.02), WOOD)
		b.box(Vector3(-0.042, 0.003, 0.004 + side * 0.008), Vector3(0.02, 0.006, 0.004), WOOD)
	kit.meshes["decor_bricks"] = b.commit()

# ------------------------------------------------------------------ works: signal, charcoal, furnace, sledge, quarry

static func _works(kit) -> void:
	# signal post: a stone-kerbed platform with a tall log-crib pyre under a tepee (the flame burns on top), a pennant (plain red: hill posts get no civ colour)
	var b: M7MeshBuilder = kit._b(707)
	b.prism(Vector3.ZERO, 0.05, 0.046, 0.012, 7, STONE_DARK, Color("#4a3e33"))
	b.tag = GLOW
	b.box(Vector3(0, 0.026, 0), Vector3(0.028, 0.026, 0.028), EMBER)
	b.tag = PLAIN
	for k in 4:
		var y := 0.0165 + k * 0.009
		var w := 0.022 - k * 0.0015
		var l := 0.03 - k * 0.002
		for s in [-1.0, 1.0]:
			if k % 2 == 0:
				b.pole(Vector3(-l, y, s * w), Vector3(l, y, s * w), 0.0048, WOOD if s < 0.0 else BARK, 3)
			else:
				b.pole(Vector3(s * w, y, -l), Vector3(s * w, y, l), 0.0048, WOOD_LIGHT if s < 0.0 else WOOD, 3)
	for k in 3:
		var a := TAU * k / 3.0 + 0.5
		b.pole(Vector3(cos(a) * 0.02, 0.05, sin(a) * 0.02), Vector3(0, 0.078, 0), 0.003, BARK, 3)
	b.pole(Vector3(0.04, 0.01, -0.03), Vector3(0.04, 0.142, -0.03), 0.003, WOOD)
	b.quad(Vector3(0.042, 0.14, -0.03), Vector3(0.072, 0.135, -0.03), Vector3(0.07, 0.12, -0.03), Vector3(0.042, 0.118, -0.03), Color("#b23a2a"))
	kit.meshes["decor_signal"] = b.commit()

	# charcoal clamp: an earth-sealed dome glowing at its vents, split billets stacked beside it, a rake and a heap of charcoal
	b = kit._b(708)
	b.blob(Vector3.ZERO, Vector3(0.045, 0.04, 0.045), 8, 4, Color("#5d5044"), 0.08, Color("#3e342c"))
	b.prism(Vector3(0, 0.038, 0), 0.009, 0.007, 0.008, 6, SOOT, SOOT)
	for lg in [[0.052, 0.005], [0.064, 0.005], [0.058, 0.014]]:
		b.pole(Vector3(lg[0], lg[1], -0.026), Vector3(lg[0], lg[1], 0.026), 0.0055, FRESH if lg[1] > 0.01 else WOOD_LIGHT, 5)
	b.blob(Vector3(-0.042, 0, 0.042), Vector3(0.019, 0.011, 0.016), 6, 2, Color("#1c1916"), 0.15)
	b.pole(Vector3(-0.068, 0, -0.014), Vector3(-0.028, 0.036, -0.016), 0.0024, WOOD_LIGHT)
	b.box(Vector3(-0.068, 0.002, -0.014), Vector3(0.004, 0.004, 0.022), WOOD)
	b.tag = GLOW
	for a in [0.9, 2.2]:
		b.box(Vector3(cos(a) * 0.042, 0.005, sin(a) * 0.042), Vector3(0.01, 0.006, 0.01), EMBER)
	b.box(Vector3(0, 0.046, 0), Vector3(0.009, 0.003, 0.009), EMBER_HI)
	kit.meshes["decor_charcoal"] = b.commit()

	# furnace: a tall clay shaft glowing at the top and the tap hole, pot bellows piped in, green ore and a copper ingot
	b = kit._b(709)
	var sp := Vector3(0.006, 0, -0.004)
	_lathe(b, sp, [[0.03, 0.0], [0.03, 0.02], [0.022, 0.06], [0.016, 0.086], [0.018, 0.09]], 7, [CLAY_DARK, CLAY, CLAY.darkened(0.1), SOOT])
	b.blob(sp + Vector3(0, 0, 0.044), Vector3(0.015, 0.003, 0.01), 5, 2, Color("#2a2420"))
	for z in [-0.016, 0.012]:
		var bp := Vector3(-0.044, 0, z)
		b.prism(bp, 0.011, 0.012, 0.012, 6, CLAY_DARK, HIDE_DARK)
		b.pole(bp + Vector3(0, 0.012, 0), bp + Vector3(0.002, 0.03, 0.002), 0.0016, WOOD, 3)
	b.pole(Vector3(-0.034, 0.008, -0.002), Vector3(-0.02, 0.014, -0.004), 0.003, CLAY_DARK)
	b.blob(Vector3(0.044, 0, -0.028), Vector3(0.016, 0.011, 0.014), 5, 2, Color("#4f9f72"), 0.18)
	b.blob(Vector3(0.05, 0, -0.008), Vector3(0.009, 0.007, 0.009), 5, 2, Color("#3d7f5c"), 0.18)
	b.box(Vector3(-0.024, 0.004, 0.044), Vector3(0.026, 0.008, 0.011), Color("#c0683a"), Color("#e89a5c"))
	b.tag = GLOW
	b.prism(sp + Vector3(0, 0.0895, 0), 0.016, 0.011, 0.002, 7, EMBER, EMBER_HI)
	b.box(sp + Vector3(0, 0.011, 0.029), Vector3(0.013, 0.013, 0.006), EMBER)
	kit.meshes["decor_furnace"] = b.commit()

	# sledge: shown side-on, a rough stone block lashed with dark rope onto slats and runners that curl up high at the front
	b = kit._b(711)
	var rope := Color("#6b5236")
	for z in [-0.028, 0.028]:
		b.pole(Vector3(-0.064, 0.004, z), Vector3(0.036, 0.004, z), 0.005, WOOD)
		b.pole(Vector3(0.036, 0.004, z), Vector3(0.058, 0.02, z), 0.0048, WOOD)
		b.pole(Vector3(0.058, 0.02, z), Vector3(0.062, 0.044, z), 0.0044, WOOD)
	b.pole(Vector3(0.061, 0.04, -0.034), Vector3(0.061, 0.04, 0.034), 0.0034, WOOD_LIGHT)
	for x in [-0.05, -0.014, 0.022]:
		b.box(Vector3(x, 0.0105, 0), Vector3(0.014, 0.004, 0.07), FRESH, FRESH.lightened(0.08))
	b.prism(Vector3(-0.014, 0.012, 0), 0.042, 0.034, 0.038, 6, ROCK, ROCK_HI, 0.0, 0.12)
	for x in [-0.034, 0.006]:
		b.box(Vector3(x, 0.032, 0), Vector3(0.004, 0.044, 0.08), rope)
	b.pole(Vector3(0.061, 0.04, 0.02), Vector3(0.068, 0.002, 0.034), 0.0018, rope, 3)
	b.pole(Vector3(0.068, 0.002, 0.034), Vector3(0.05, 0.002, 0.05), 0.0018, rope, 3)
	kit.meshes["decor_sledge"] = b.commit()

	# quarry: a jagged grey outcrop, a stack of pale squared blocks cut from it, a pick leaning on them, a maul and chips
	b = kit._b(716)
	var cut := Color("#c6b9a0")
	b.prism(Vector3(-0.02, 0, -0.034), 0.046, 0.018, 0.062, 6, STONE_DARK, STONE, 0.3, 0.22)
	b.prism(Vector3(0.03, 0, -0.04), 0.03, 0.012, 0.04, 5, STONE, ROCK_HI, 0.1, 0.25)
	b.box(Vector3(-0.004, 0.009, 0.026), Vector3(0.03, 0.018, 0.022), cut, cut.lightened(0.1))
	b.box(Vector3(0.03, 0.009, 0.022), Vector3(0.03, 0.018, 0.022), cut.darkened(0.06), cut.lightened(0.06))
	b.box(Vector3(0.012, 0.027, 0.024), Vector3(0.032, 0.018, 0.022), cut.lightened(0.04), cut.lightened(0.14))
	b.pole(Vector3(0.056, 0, 0.044), Vector3(0.036, 0.05, 0.03), 0.0026, WOOD_LIGHT)
	_at(b, Vector3(0.036, 0.05, 0.03), 0.6, 0.0, 0.35)
	b.box(Vector3.ZERO, Vector3(0.026, 0.005, 0.005), STONE_DARK.darkened(0.2))
	_home(b)
	b.pole(Vector3(-0.056, 0.003, 0.03), Vector3(-0.032, 0.003, 0.052), 0.0025, WOOD_LIGHT)
	b.box(Vector3(-0.058, 0.006, 0.028), Vector3(0.012, 0.012, 0.012), STONE_DARK)
	for rb in [Vector3(0.056, 0, -0.006), Vector3(-0.03, 0, 0.004), Vector3(-0.022, 0, 0.054)]:
		b.prism(rb, 0.007, 0.0, 0.006, 3, cut, cut, rb.x * 40.0)
	kit.meshes["decor_quarry"] = b.commit()

# ------------------------------------------------------------------ camp: spear rack

static func _camp(kit) -> void:
	# spear rack: long flint spears leaning on an X-legged rack, a civ-painted hide shield propped in front
	var b: M7MeshBuilder = kit._b(713)
	for x in [-0.052, 0.052]:
		b.pole(Vector3(x, 0, -0.02), Vector3(x, 0.08, 0.014), 0.0034, WOOD)
		b.pole(Vector3(x, 0, 0.02), Vector3(x, 0.08, -0.014), 0.0034, WOOD)
	b.pole(Vector3(-0.06, 0.06, 0), Vector3(0.06, 0.06, 0), 0.003, WOOD_LIGHT)
	var flint := Color("#3b3733")
	for i in 6:
		var x := -0.04 + i * 0.016
		var base := Vector3(x, 0, 0.026)
		var tip := Vector3(x * 0.92, 0.138, -0.034)
		b.pole(base, tip, 0.0022, FRESH if i % 2 == 0 else WOOD_LIGHT, 3)
		var d := (tip - base).normalized()
		_at(b, tip, 0.0, atan2(d.z, d.y))
		b.prism(Vector3.ZERO, 0.0056, 0.0, 0.02, 4, flint, flint)
		_home(b)
	_at(b, Vector3(0.016, 0.029, 0.034), 0.0, PI * 0.5 - 0.35)
	b.prism(Vector3(0, -0.002, 0), 0.027, 0.027, 0.003, 8, HIDE_DARK, HIDE, PI / 8.0)
	b.tag = CIV
	b.prism(Vector3(0, 0.0012, 0), 0.019, 0.017, 0.0012, 8, Color.WHITE, Color.WHITE, PI / 8.0)
	b.tag = PLAIN
	b.prism(Vector3(0, 0.0024, 0), 0.006, 0.0, 0.006, 5, SOOT, SOOT)
	_home(b)
	kit.meshes["decor_spearrack"] = b.commit()
