## Every prop, building, figure and animal, generated in code (no assets).
## Units: 1.0 = one tile column. An adult figure is ~0.1 tall, a hut ~0.15.
class_name M7MeshKit
extends RefCounted


# palette (sRGB)
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const BARK := Color("#4b3a2a")
const THATCH := Color("#c29a52")
const THATCH_DARK := Color("#a07c3e")
const HIDE := Color("#b99a72")
const HIDE_DARK := Color("#8f7354")
const DAUB := Color("#c2a57a")
const MUD := Color("#b98a5e")
const EARTH := Color("#7b6247")
const STONE := Color("#8f8a80")
const STONE_DARK := Color("#6c675f")
const SKIN := Color("#b07a52")
const HAIR := Color("#2b201a")
const CREAM := Color("#f3ead6")
const LEAF := Color("#5a8a34")
const LEAF_DARK := Color("#3f6e2c")
const CONIFER := Color("#2f5a3c")
const CONIFER_LIGHT := Color("#3d6d45")
const ACACIA := Color("#6f8c3a")
const JUNGLE := Color("#2f6e30")
const GRASS := Color("#8ea548")
const DRYGRASS := Color("#b8a656")
const REED := Color("#7f8f48")
const SOIL := Color("#7a5a3e")
const ROCK := Color("#8e877c")
const ROCK_HI := Color("#a39c90")
const SNOW := Color("#eef3f7")

var meshes := {}
var far_tree_meshes := {}

func _init() -> void:
	_trees()
	_ground_cover()
	_mountains()
	_structures()
	_decor()
	_monument()
	_boats()
	_feast()
	_dogs()
	_people()
	_animals()

func get_mesh(name: String) -> ArrayMesh:
	return meshes[name]

func _b(seed: int = 1) -> M7MeshBuilder:
	var b := M7MeshBuilder.new()
	b.seed = seed
	return b

# ------------------------------------------------------------------ vegetation

func _trees() -> void:
	for k in 3:
		var b := _b(11 + k * 7)
		b.prism(Vector3.ZERO, 0.016, 0.012, 0.07, 5, BARK, BARK)
		b.tag = M7MeshBuilder.TAG_EVERGREEN
		var tiers := 3 + (1 if k == 2 else 0)
		var y := 0.045
		var r := 0.105 + 0.01 * k
		for i in tiers:
			var hh := 0.12 - i * 0.012
			var col := CONIFER.lerp(CONIFER_LIGHT, float(i) / tiers)
			b.prism(Vector3(0, y, 0), r, 0.0, hh, 7, col, col, b.rnd() * TAU, 0.12, true)
			y += hh * 0.55
			r *= 0.76
		meshes["conifer%d" % k] = b.commit()
	for k in 3:
		var b := _b(31 + k * 5)
		b.prism(Vector3.ZERO, 0.017, 0.012, 0.09, 5, BARK, BARK)
		b.pole(Vector3(0, 0.06, 0), Vector3(0.035, 0.11, 0.01), 0.008, BARK)
		b.tag = M7MeshBuilder.TAG_FOLIAGE
		b.blob(Vector3(0, 0.15, 0), Vector3(0.085, 0.075, 0.085), 7, 4, LEAF, 0.14, LEAF_DARK)
		b.blob(Vector3(0.05, 0.12, 0.02), Vector3(0.06, 0.055, 0.06), 6, 3, LEAF.lightened(0.05), 0.15, LEAF_DARK)
		if k > 0:
			b.blob(Vector3(-0.04, 0.125, -0.03), Vector3(0.055, 0.05, 0.055), 6, 3, LEAF, 0.15, LEAF_DARK)
		meshes["broadleaf%d" % k] = b.commit()
	for k in 2:
		# acacia: leaning trunk, forked, flat umbrella canopy
		var b := _b(51 + k * 3)
		var top := Vector3(0.02 * (1 - 2 * k), 0.13, 0.01)
		b.pole(Vector3.ZERO, top, 0.011, BARK)
		b.pole(top, top + Vector3(0.05, 0.04, 0.0), 0.007, BARK)
		b.pole(top, top + Vector3(-0.045, 0.035, 0.02), 0.007, BARK)
		b.tag = M7MeshBuilder.TAG_FOLIAGE
		b.blob(top + Vector3(0.0, 0.05, 0.0), Vector3(0.13, 0.03, 0.105), 8, 3, ACACIA, 0.1, ACACIA.darkened(0.25))
		b.blob(top + Vector3(0.05, 0.065, 0.01), Vector3(0.07, 0.022, 0.06), 6, 3, ACACIA.lightened(0.06), 0.1, ACACIA.darkened(0.2))
		meshes["acacia%d" % k] = b.commit()
	for k in 2:
		var b := _b(71 + k * 9)
		b.prism(Vector3.ZERO, 0.02, 0.013, 0.17, 5, BARK.lightened(0.1), BARK)
		b.tag = M7MeshBuilder.TAG_EVERGREEN
		b.blob(Vector3(0, 0.2, 0), Vector3(0.1, 0.07, 0.1), 7, 4, JUNGLE, 0.18, JUNGLE.darkened(0.3))
		b.blob(Vector3(0.06, 0.16, 0.03), Vector3(0.07, 0.05, 0.07), 6, 3, JUNGLE.lightened(0.08), 0.18, JUNGLE.darkened(0.3))
		b.blob(Vector3(-0.05, 0.17, -0.04), Vector3(0.065, 0.05, 0.065), 6, 3, JUNGLE.lightened(0.04), 0.18, JUNGLE.darkened(0.3))
		meshes["jungle%d" % k] = b.commit()
	for kind in ["conifer", "broadleaf", "acacia", "jungle"]:
		var variant_count := 2 if kind == "acacia" or kind == "jungle" else 3
		for variant in variant_count:
			far_tree_meshes["%s%d" % [kind, variant]] = _make_far_tree(kind, variant)
	# bushes
	var bb := _b(91)
	bb.tag = M7MeshBuilder.TAG_FOLIAGE
	bb.blob(Vector3(0, 0.025, 0), Vector3(0.05, 0.035, 0.045), 6, 3, LEAF.darkened(0.1), 0.2, LEAF_DARK.darkened(0.1))
	bb.blob(Vector3(0.035, 0.018, 0.01), Vector3(0.03, 0.025, 0.03), 5, 3, LEAF, 0.2)
	meshes["bush"] = bb.commit()
	var bt := _b(93)
	bt.tag = M7MeshBuilder.TAG_FOLIAGE
	bt.blob(Vector3(0, 0.018, 0), Vector3(0.035, 0.022, 0.032), 6, 3, Color("#7d7a45"), 0.3, Color("#5b5a35"))
	meshes["shrub"] = bt.commit()
	# orchard tree: small round canopy with fruit
	var ob := _b(95)
	ob.prism(Vector3.ZERO, 0.009, 0.007, 0.05, 5, BARK, BARK)
	ob.tag = M7MeshBuilder.TAG_FOLIAGE
	ob.blob(Vector3(0, 0.075, 0), Vector3(0.045, 0.04, 0.045), 7, 4, LEAF.lightened(0.05), 0.08, LEAF_DARK)
	ob.tag = M7MeshBuilder.TAG_PLAIN
	for i in 5:
		var a := TAU * i / 5.0 + 0.3
		ob.blob(Vector3(cos(a) * 0.038, 0.07 + 0.015 * sin(a * 3.0), sin(a) * 0.038), Vector3(0.008, 0.008, 0.008), 4, 2, Color("#c0392b"))
	meshes["orchard_tree"] = ob.commit()
	far_tree_meshes["orchard_tree"] = _make_far_tree("orchard", 0)

func get_far_tree_mesh(name: String) -> ArrayMesh:
	return far_tree_meshes.get(name)

func _make_far_tree(kind: String, variant: int) -> ArrayMesh:
	# The far camera covers many more trees at once. Keep their silhouettes and colours,
	# but use a single canopy form so the 80k vegetation instances stay inexpensive.
	var b := _b(811 + variant * 17 + kind.length() * 31)
	match kind:
		"conifer":
			b.prism(Vector3.ZERO, 0.013, 0.008, 0.065, 4, BARK, BARK)
			b.tag = M7MeshBuilder.TAG_EVERGREEN
			b.prism(Vector3(0, 0.035, 0), 0.105 + variant * 0.006, 0.0, 0.18 + variant * 0.012,
				5, CONIFER.lerp(CONIFER_LIGHT, variant * 0.12), CONIFER_LIGHT, 0.15, 0.06)
		"broadleaf":
			b.prism(Vector3.ZERO, 0.015, 0.01, 0.10, 4, BARK, BARK)
			b.tag = M7MeshBuilder.TAG_FOLIAGE
			b.blob(Vector3(0, 0.15, 0), Vector3(0.09 + variant * 0.008, 0.075, 0.09),
				6, 2, LEAF, 0.08, LEAF_DARK)
		"acacia":
			var top := Vector3(0.0, 0.13, 0.0)
			b.pole(Vector3.ZERO, top, 0.011, BARK)
			b.pole(top, top + Vector3(0.05, 0.04, 0.0), 0.007, BARK)
			b.pole(top, top + Vector3(-0.045, 0.035, 0.02), 0.007, BARK)
			b.tag = M7MeshBuilder.TAG_FOLIAGE
			b.blob(top + Vector3(0.0, 0.05, 0.0), Vector3(0.13 + variant * 0.008, 0.03, 0.105),
				6, 2, ACACIA, 0.06, ACACIA.darkened(0.25))
		"jungle":
			b.prism(Vector3.ZERO, 0.02, 0.013, 0.17, 4, BARK, BARK)
			b.tag = M7MeshBuilder.TAG_FOLIAGE
			b.blob(Vector3(0, 0.20, 0), Vector3(0.115 + variant * 0.008, 0.075, 0.11),
				6, 2, JUNGLE, 0.12, JUNGLE.darkened(0.3))
		"orchard":
			b.prism(Vector3.ZERO, 0.009, 0.007, 0.05, 4, BARK, BARK)
			b.tag = M7MeshBuilder.TAG_FOLIAGE
			b.blob(Vector3(0, 0.075, 0), Vector3(0.045, 0.04, 0.045), 6, 2,
				LEAF.lightened(0.05), 0.05, LEAF_DARK)
	return b.commit()

func _ground_cover() -> void:
	for k in 2:
		var b := _b(101 + k)
		b.tag = M7MeshBuilder.TAG_GRASS
		var col := GRASS if k == 0 else DRYGRASS
		for i in 5:
			var a := b.rnd() * TAU
			var r := 0.012 * b.rnd()
			var base := Vector3(cos(a) * r, 0, sin(a) * r)
			var h := 0.035 + 0.025 * b.rnd()
			var lean := Vector3(cos(a) * 0.012, h, sin(a) * 0.012)
			var side := Vector3(-sin(a), 0, cos(a)) * 0.005
			b.tri(base - side, base + side, base + lean, col.darkened(0.15 * b.rnd()))
		meshes["tuft%d" % k] = b.commit()
	for k in 2:
		var g := _b(141 + k)
		g.tag = M7MeshBuilder.TAG_GRASS
		var gc := Color("#9a8c40") if k == 0 else Color("#6f8a3a")
		for i in 11:
			var a := g.rnd() * TAU
			var r := 0.03 * sqrt(g.rnd())
			var base := Vector3(cos(a) * r, 0, sin(a) * r)
			var h := 0.035 + 0.035 * g.rnd()
			var lean := Vector3(cos(a) * 0.018, h, sin(a) * 0.018)
			var side := Vector3(-sin(a), 0, cos(a)) * 0.006
			g.tri(base - side, base + side, base + lean, gc.darkened(0.2 * g.rnd()).lightened(0.1 * g.rnd()))
		meshes["tallgrass%d" % k] = g.commit()
	var rb := _b(121)
	rb.tag = M7MeshBuilder.TAG_GRASS
	for i in 9:
		var a := rb.rnd() * TAU
		var r := 0.03 * rb.rnd()
		var base := Vector3(cos(a) * r, 0, sin(a) * r)
		var h := 0.06 + 0.04 * rb.rnd()
		var side := Vector3(-sin(a), 0, cos(a)) * 0.004
		rb.tri(base - side, base + side, base + Vector3(cos(a) * 0.01, h, sin(a) * 0.01), REED)
		if i % 3 == 0:
			rb.tag = M7MeshBuilder.TAG_PLAIN
			rb.blob(base + Vector3(cos(a) * 0.009, h * 0.85, sin(a) * 0.009), Vector3(0.004, 0.012, 0.004), 4, 2, Color("#6b4a2e"))
			rb.tag = M7MeshBuilder.TAG_GRASS
	meshes["reeds"] = rb.commit()
	for k in 3:
		var b := _b(131 + k * 3)
		var sc := Vector3(0.055, 0.035 + 0.01 * k, 0.045)
		b.blob(Vector3(0, sc.y * 0.4, 0), sc, 5, 3, ROCK.lerp(ROCK_HI, 0.3 * k), 0.28, ROCK.darkened(0.2))
		if k == 2:
			b.blob(Vector3(0.05, 0.012, 0.02), Vector3(0.025, 0.02, 0.022), 5, 3, ROCK, 0.3)
		meshes["rock%d" % k] = b.commit()

func _mountains() -> void:
	for k in 6:
		var big := k >= 3
		var b := _b(201 + k * 13)
		var h := (1.7 if big else 1.0) * (0.85 + 0.3 * b.rnd())
		_peak(b, Vector3(0.03 * b.rnds(), 0, 0.03 * b.rnds()), 0.72, h, 8, 0.55 if big else 0.86)
		# shoulders: one or two lower peaks offset from the main one
		var n2 := 1 + (k % 2)
		for i in n2:
			var a := b.rnd() * TAU
			var off := Vector3(cos(a), 0, sin(a)) * (0.22 + 0.08 * b.rnd())
			_peak(b, off, 0.45, h * (0.45 + 0.25 * b.rnd()), 6, 0.8 if big else 1.2)
		meshes["mountain%d" % k] = b.commit()

## A faceted peak: rings of jittered vertices, colour banded by height (scree, rock, snow).
func _peak(b: M7MeshBuilder, p: Vector3, r: float, h: float, sides: int, snow_frac: float) -> void:
	var rings := [[1.0, 0.0], [0.72, 0.22], [0.45, 0.5], [0.2, 0.78]]
	var pts := []
	var rot := b.rnd() * TAU
	for ri in rings.size():
		var row := []
		var rr: float = rings[ri][0] * r
		var yy: float = rings[ri][1] * h
		for s in sides:
			var a := rot + TAU * (s + 0.5 * (ri % 2)) / sides
			var j := 1.0 + 0.25 * b.rnds()
			row.append(p + Vector3(cos(a) * rr * j, yy + (0.05 * h * b.rnds() if ri > 0 else -0.02), sin(a) * rr * j))
		pts.append(row)
	var tip := p + Vector3(0.06 * r * b.rnds(), h, 0.06 * r * b.rnds())
	var snow_y := p.y + h * snow_frac
	var foot := Color("#5f6a45")
	var rock0 := Color("#6b6158")
	var rock1 := Color("#8c8274")
	var cen_axis := p + Vector3(0, h * 0.3, 0)
	for ri in rings.size():
		for s in sides:
			var s1 := (s + 1) % sides
			var a: Vector3 = pts[ri][s]
			var bq: Vector3 = pts[ri][s1]
			if ri == rings.size() - 1:
				var cen := (a + bq + tip) / 3.0
				b.tri(a, bq, tip, SNOW if cen.y > snow_y - 0.05 else rock1, cen_axis)
				continue
			var c: Vector3 = pts[ri + 1][s]
			var d: Vector3 = pts[ri + 1][s1]
			var mid := (a + bq + c + d) * 0.25
			var f := clampf((mid.y - p.y) / h, 0.0, 1.0)
			var colr: Color = rock0.lerp(rock1, f)
			if ri == 0:
				colr = foot.lerp(rock0, 0.3 * b.rnd())
			if mid.y > snow_y:
				colr = SNOW
			elif mid.y > snow_y - 0.1 * h and b.rnd() > 0.5:
				colr = SNOW.darkened(0.06)
			var shade := 0.06 * b.rnds()
			b.tri(a, bq, d, colr.lightened(maxf(shade, 0.0)).darkened(maxf(-shade, 0.0)), cen_axis)
			b.tri(a, d, c, colr.darkened(0.05), cen_axis)

# ------------------------------------------------------------------ structures

func _structures() -> void:
	# hearth: stone ring, logs, ember bed
	var b := _b(301)
	for i in 8:
		var a := TAU * i / 8.0
		b.blob(Vector3(cos(a) * 0.036, 0.006, sin(a) * 0.036), Vector3(0.011, 0.008, 0.01), 5, 2, STONE.lerp(STONE_DARK, b.rnd()), 0.2)
	for i in 3:
		var a := TAU * i / 3.0
		b.pole(Vector3(cos(a) * 0.03, 0.003, sin(a) * 0.03), Vector3(cos(a) * 0.004, 0.02, sin(a) * 0.004), 0.0045, WOOD)
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	b.prism(Vector3(0, 0.0, 0), 0.024, 0.02, 0.006, 6, Color("#ff5a1a"), Color("#ff7a20"))
	meshes["hearth"] = b.commit()

	b = _b(303)
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	b.prism(Vector3.ZERO, 0.02, 0.0, 0.075, 5, Color("#ff6a10"), Color("#ff6a10"), 0.0, 0.2)
	b.prism(Vector3(0.006, 0, 0.004), 0.012, 0.0, 0.05, 5, Color("#ffc040"), Color("#ffc040"), 0.5, 0.2)
	b.prism(Vector3(-0.008, 0, -0.003), 0.01, 0.0, 0.04, 4, Color("#ff9a20"), Color("#ff9a20"), 0.2, 0.2)
	meshes["flame"] = b.commit()

	# Dwellings, oldest to newest. Each reads differently from the others at map distance: the lean-to is a wedge of boughs,
	# the hide tent a cone, the pit house a turf mound, the wattle house a low cottage with a hipped roof, the mudbrick house a
	# flat-roofed stack of blocks, the longhouse a long gable and the hall a colonnaded block. Doors face +Z (toward the hearth).
	var BOUGH := Color("#6f7f3a")
	var BOUGH_DARK := Color("#4d5f2c")
	var DARK := Color("#2a2119")
	var DOOR := Color("#3a2c1f")
	var BRICK := Color("#c99a68")
	var BRICK_LIGHT := Color("#dcb98a")
	var LIME := Color("#e2cfa6")

	# lean-to: one sloping pitch of leafy boughs on forked posts, open to the hearth, bedding underneath
	b = _b(305)
	b.quad(Vector3(-0.085, 0.092, 0.06), Vector3(0.085, 0.092, 0.06), Vector3(0.085, 0.03, -0.058), Vector3(-0.085, 0.03, -0.058), BOUGH, Vector3(0, -0.04, 0))
	for i in 7:
		var x := -0.078 + 0.026 * i
		b.pole(Vector3(x, 0.098, 0.068), Vector3(x + 0.004 * b.rnds(), 0.026, -0.064), 0.0045, WOOD if i % 2 == 0 else BOUGH_DARK)
	for sx in [-0.085, 0.085]:
		b.quad(Vector3(sx, 0.092, 0.06), Vector3(sx, 0.03, -0.058), Vector3(sx, 0.0, -0.058), Vector3(sx, 0.0, 0.06), BOUGH_DARK, Vector3(0, 0.04, 0))
		b.pole(Vector3(sx, 0.0, 0.062), Vector3(sx, 0.1, 0.062), 0.006, WOOD)
	b.quad(Vector3(-0.085, 0.03, -0.058), Vector3(0.085, 0.03, -0.058), Vector3(0.085, 0.0, -0.058), Vector3(-0.085, 0.0, -0.058), BOUGH_DARK, Vector3(0, 0.04, 0.1))
	b.blob(Vector3(0.0, 0.0, 0.0), Vector3(0.06, 0.016, 0.04), 7, 3, THATCH_DARK, 0.15)
	b.blob(Vector3(0.03, 0.0, 0.085), Vector3(0.022, 0.014, 0.014), 5, 2, BOUGH, 0.2)
	meshes["leanto"] = b.commit()

	# hide tent (tipi) with a civ band and protruding poles
	b = _b(307)
	b.prism(Vector3.ZERO, 0.062, 0.012, 0.13, 8, HIDE, HIDE)
	b.tag = M7MeshBuilder.TAG_CIV
	var band_y0 := 0.055
	var band_y1 := 0.075
	for i in 8:
		var a0 := TAU * i / 8.0
		var a1 := TAU * (i + 1) / 8.0
		var rr0 := 0.062 - (0.05) * band_y0 / 0.13 + 0.0015
		var rr1 := 0.062 - (0.05) * band_y1 / 0.13 + 0.0015
		b.quad(Vector3(cos(a0) * rr0, band_y0, sin(a0) * rr0), Vector3(cos(a1) * rr0, band_y0, sin(a1) * rr0),
			Vector3(cos(a1) * rr1, band_y1, sin(a1) * rr1), Vector3(cos(a0) * rr1, band_y1, sin(a0) * rr1), Color.WHITE, Vector3(0, 0.065, 0))
	b.tag = M7MeshBuilder.TAG_PLAIN
	for i in 4:
		var a := TAU * i / 4.0 + 0.4
		b.pole(Vector3(cos(a) * 0.008, 0.12, sin(a) * 0.008), Vector3(cos(a) * -0.012, 0.17, sin(a) * -0.012), 0.0035, WOOD)
	# door flap (dark triangle facing +Z)
	b.tri(Vector3(-0.022, 0.001, 0.058), Vector3(0.022, 0.001, 0.058), Vector3(0.0, 0.055, 0.044), Color("#3a2c1f"), Vector3(0, 0.03, 0))
	meshes["tent"] = b.commit()

	# pit house: a dug floor under a turf-covered log roof, rim stones, a ladder out of the smoke hole and a sloping entrance
	b = _b(309)
	b.prism(Vector3.ZERO, 0.105, 0.098, 0.01, 10, EARTH.darkened(0.25), EARTH.darkened(0.25))
	b.tag = M7MeshBuilder.TAG_GRASS
	b.blob(Vector3(0, 0.008, 0), Vector3(0.088, 0.058, 0.088), 10, 4, Color("#7f8c48"), 0.07, EARTH)
	b.tag = M7MeshBuilder.TAG_PLAIN
	for i in 10:
		var a := TAU * i / 10.0 + 0.2
		b.blob(Vector3(cos(a) * 0.098, 0.008, sin(a) * 0.098), Vector3(0.014, 0.012, 0.012), 5, 2, STONE.lerp(STONE_DARK, b.rnd()), 0.2)
	b.prism(Vector3(0, 0.058, 0), 0.02, 0.016, 0.012, 6, DARK, DARK)
	b.pole(Vector3(-0.008, 0.03, -0.004), Vector3(-0.012, 0.125, 0.006), 0.004, WOOD)
	b.pole(Vector3(0.008, 0.03, -0.004), Vector3(0.006, 0.125, 0.008), 0.004, WOOD)
	for r in 3:
		b.box(Vector3(-0.003 + 0.0 * r, 0.06 + 0.02 * r, 0.0), Vector3(0.022, 0.003, 0.004), WOOD_LIGHT)
	b.quad(Vector3(-0.026, 0.0, 0.13), Vector3(0.026, 0.0, 0.13), Vector3(0.026, 0.036, 0.09), Vector3(-0.026, 0.036, 0.09), EARTH.darkened(0.2), Vector3(0, -0.1, 0))
	b.box(Vector3(-0.026, 0.02, 0.098), Vector3(0.006, 0.04, 0.006), WOOD)
	b.box(Vector3(0.026, 0.02, 0.098), Vector3(0.006, 0.04, 0.006), WOOD)
	b.box(Vector3(0, 0.044, 0.098), Vector3(0.06, 0.007, 0.008), WOOD)
	b.box(Vector3(0, 0.02, 0.092), Vector3(0.044, 0.036, 0.003), DARK)
	meshes["pithouse"] = b.commit()

	# wattle-and-daub house: a low timber-framed cottage, plastered wicker walls, hipped thatch with deep eaves
	b = _b(311)
	var wx := 0.07
	var wz := 0.052
	b.box(Vector3(0, 0.02, 0), Vector3(wx * 2, 0.04, wz * 2), DAUB)
	b.box(Vector3(0, 0.0095, 0), Vector3(wx * 2 + 0.01, 0.019, wz * 2 + 0.01), STONE_DARK)
	for sx in [-wx, wx]:
		for sz in [-wz, wz]:
			b.pole(Vector3(sx, 0.0, sz), Vector3(sx, 0.047, sz), 0.0055, WOOD)
	for sx in [-0.024, 0.024]:
		b.box(Vector3(sx, 0.02, wz + 0.001), Vector3(0.005, 0.04, 0.003), WOOD)
	b.box(Vector3(0, 0.0435, wz + 0.001), Vector3(wx * 2, 0.006, 0.004), WOOD)
	b.box(Vector3(0, 0.0175, wz + 0.003), Vector3(0.026, 0.035, 0.003), DOOR)
	b.box(Vector3(-0.045, 0.028, wz + 0.002), Vector3(0.016, 0.01, 0.003), DARK)
	var ey := 0.04
	var ry2 := 0.118
	var ox := 0.088
	var oz := 0.07
	var rx := 0.03
	b.quad(Vector3(-ox, ey, oz), Vector3(ox, ey, oz), Vector3(rx, ry2, 0), Vector3(-rx, ry2, 0), THATCH, Vector3(0, 0.02, 0))
	b.quad(Vector3(-ox, ey, -oz), Vector3(ox, ey, -oz), Vector3(rx, ry2, 0), Vector3(-rx, ry2, 0), THATCH.darkened(0.06), Vector3(0, 0.02, 0))
	b.tri(Vector3(-ox, ey, oz), Vector3(-ox, ey, -oz), Vector3(-rx, ry2, 0), THATCH_DARK, Vector3(0, 0.02, 0))
	b.tri(Vector3(ox, ey, oz), Vector3(ox, ey, -oz), Vector3(rx, ry2, 0), THATCH_DARK, Vector3(0, 0.02, 0))
	b.pole(Vector3(-rx - 0.008, ry2 + 0.004, 0), Vector3(rx + 0.008, ry2 + 0.004, 0), 0.006, THATCH_DARK)
	b.pole(Vector3(-0.07, 0.07, 0.04), Vector3(0.07, 0.07, 0.04), 0.0035, THATCH_DARK)
	b.prism(Vector3(-0.012, 0.104, -0.012), 0.01, 0.008, 0.016, 5, STONE_DARK, DARK)
	meshes["wattle"] = b.commit()

	# mudbrick house: sun-baked blocks with flat roofs, a second room stepped on top, beam ends and a roof ladder
	b = _b(313)
	b.box(Vector3(0, 0.038, 0), Vector3(0.14, 0.076, 0.1), BRICK)
	b.box(Vector3(0, 0.079, 0), Vector3(0.15, 0.008, 0.11), BRICK_LIGHT.darkened(0.08), BRICK_LIGHT)
	b.box(Vector3(-0.03, 0.1, -0.008), Vector3(0.078, 0.042, 0.078), BRICK_LIGHT)
	b.box(Vector3(-0.03, 0.124, -0.008), Vector3(0.088, 0.008, 0.088), BRICK_LIGHT.darkened(0.08), BRICK_LIGHT)
	for i in 4:
		b.pole(Vector3(-0.054 + 0.036 * i, 0.068, 0.049), Vector3(-0.054 + 0.036 * i, 0.068, 0.067), 0.0045, WOOD)
	b.box(Vector3(0.025, 0.027, 0.0505), Vector3(0.028, 0.054, 0.003), DOOR)
	b.box(Vector3(0.025, 0.056, 0.0505), Vector3(0.034, 0.005, 0.004), WOOD)
	b.box(Vector3(-0.04, 0.045, 0.0505), Vector3(0.016, 0.014, 0.003), DARK)
	b.box(Vector3(-0.03, 0.105, 0.0315), Vector3(0.016, 0.016, 0.003), DARK)
	b.pole(Vector3(0.071, 0.0, 0.03), Vector3(0.071, 0.088, -0.005), 0.003, WOOD)
	b.pole(Vector3(0.081, 0.0, 0.03), Vector3(0.081, 0.088, -0.005), 0.003, WOOD)
	for r in 4:
		b.pole(Vector3(0.071, 0.014 + 0.019 * r, 0.026 - 0.0085 * r), Vector3(0.081, 0.014 + 0.019 * r, 0.026 - 0.0085 * r), 0.0022, WOOD_LIGHT)
	b.blob(Vector3(0.05, 0.088, 0.02), Vector3(0.016, 0.014, 0.016), 6, 3, EARTH.lightened(0.15), 0.1)
	meshes["mudbrick"] = b.commit()

	# longhouse: a long timber hall under a steep thatched gable, posts along the walls, crossed ridge poles, smoke holes
	b = _b(315)
	var lx := 0.15
	var lz := 0.05
	b.box(Vector3(0, 0.0125, 0), Vector3(lx * 2, 0.025, lz * 2), STONE_DARK)
	b.box(Vector3(0, 0.0435, 0), Vector3(lx * 2 - 0.01, 0.037, lz * 2 - 0.008), WOOD_LIGHT)
	for i in 7:
		var x := -lx + 0.015 + (lx * 2 - 0.03) * i / 6.0
		for sz in [-lz, lz]:
			b.pole(Vector3(x, 0.0, sz), Vector3(x, 0.066, sz), 0.0045, WOOD)
	var le := 0.062
	var lr := 0.148
	var lw := 0.082
	b.quad(Vector3(-lx - 0.01, le, lw), Vector3(lx + 0.01, le, lw), Vector3(lx + 0.01, lr, 0), Vector3(-lx - 0.01, lr, 0), THATCH, Vector3(0, 0.03, 0))
	b.quad(Vector3(-lx - 0.01, le, -lw), Vector3(lx + 0.01, le, -lw), Vector3(lx + 0.01, lr, 0), Vector3(-lx - 0.01, lr, 0), THATCH.darkened(0.06), Vector3(0, 0.03, 0))
	for sz in [-1.0, 1.0]:
		for f in [0.36, 0.68]:
			b.pole(Vector3(-lx - 0.008, le + (lr - le) * f, lw * (1.0 - f) * sz), Vector3(lx + 0.008, le + (lr - le) * f, lw * (1.0 - f) * sz), 0.0035, THATCH_DARK)
	for sx in [-lx, lx]:
		b.tri(Vector3(sx, 0.062, lz - 0.004), Vector3(sx, 0.062, -lz + 0.004), Vector3(sx, lr - 0.012, 0), WOOD_LIGHT, Vector3(0, 0.05, 0))
		var d := -1.0 if sx < 0.0 else 1.0
		b.pole(Vector3(sx + d * 0.012, lr + 0.03, 0.0), Vector3(sx - d * 0.004, lr - 0.03, 0.02), 0.0045, WOOD)
		b.pole(Vector3(sx + d * 0.012, lr + 0.03, 0.0), Vector3(sx - d * 0.004, lr - 0.03, -0.02), 0.0045, WOOD)
	b.box(Vector3(0.0, 0.025, lz + 0.001), Vector3(0.03, 0.05, 0.003), DOOR)
	b.box(Vector3(-0.07, 0.042, lz + 0.001), Vector3(0.018, 0.012, 0.003), DARK)
	b.box(Vector3(0.07, 0.042, lz + 0.001), Vector3(0.018, 0.012, 0.003), DARK)
	for sx in [-0.05, 0.06]:
		b.box(Vector3(sx, 0.1, 0.0), Vector3(0.022, 0.014, 0.03), DARK)
	meshes["longhouse"] = b.commit()

	# hall: stone footing, plastered mudbrick body, a raised clerestory, timber colonnade across the front and a civ pennant
	b = _b(316)
	b.box(Vector3(0, 0.013, 0), Vector3(0.34, 0.026, 0.215), STONE)
	b.box(Vector3(0, 0.0, 0.125), Vector3(0.1, 0.002, 0.04), STONE_DARK)
	b.box(Vector3(0, 0.0725, -0.01), Vector3(0.31, 0.093, 0.165), LIME)
	b.box(Vector3(0, 0.1235, -0.01), Vector3(0.325, 0.01, 0.18), LIME.darkened(0.12), LIME.lightened(0.04))
	b.box(Vector3(0, 0.151, -0.01), Vector3(0.2, 0.046, 0.1), LIME)
	b.box(Vector3(0, 0.176, -0.01), Vector3(0.214, 0.008, 0.114), LIME.darkened(0.12), LIME.lightened(0.04))
	for i in 5:
		b.box(Vector3(-0.08 + 0.04 * i, 0.152, 0.0405), Vector3(0.018, 0.022, 0.003), DARK)
	for i in 7:
		b.pole(Vector3(-0.135 + 0.045 * i, 0.112, 0.0725), Vector3(-0.135 + 0.045 * i, 0.112, 0.092), 0.0045, WOOD)
	for i in 5:
		var x := -0.12 + 0.06 * i
		b.prism(Vector3(x, 0.026, 0.118), 0.0095, 0.008, 0.088, 6, WOOD_LIGHT, WOOD_LIGHT)
		b.box(Vector3(x, 0.027, 0.118), Vector3(0.022, 0.004, 0.022), STONE_DARK)
	b.box(Vector3(0, 0.118, 0.118), Vector3(0.27, 0.012, 0.018), WOOD)
	b.box(Vector3(0, 0.126, 0.1), Vector3(0.29, 0.007, 0.056), THATCH_DARK, THATCH)
	b.box(Vector3(0, 0.063, 0.0735), Vector3(0.06, 0.074, 0.003), DOOR)
	b.box(Vector3(0, 0.103, 0.0735), Vector3(0.07, 0.006, 0.004), WOOD)
	for sx in [-0.095, 0.095]:
		b.box(Vector3(sx, 0.07, 0.0735), Vector3(0.026, 0.026, 0.003), DARK)
	for i in 8:
		b.box(Vector3(-0.15 + 0.0429 * i, 0.131, -0.088), Vector3(0.02, 0.012, 0.012), LIME.darkened(0.06))
	b.pole(Vector3(0.0, 0.18, -0.01), Vector3(0.0, 0.25, -0.01), 0.0035, WOOD)
	b.tag = M7MeshBuilder.TAG_CIV
	b.quad(Vector3(0.002, 0.248, -0.01), Vector3(0.056, 0.236, -0.01), Vector3(0.056, 0.206, -0.01), Vector3(0.002, 0.218, -0.01), Color.WHITE, Vector3(0, 0.23, 0.3))
	b.tag = M7MeshBuilder.TAG_PLAIN
	meshes["hall"] = b.commit()

	# storage pit: stone-lined lid mound
	b = _b(317)
	b.prism(Vector3.ZERO, 0.032, 0.028, 0.01, 7, STONE, STONE.lightened(0.1), 0.0, 0.1)
	b.prism(Vector3(0, 0.01, 0), 0.024, 0.006, 0.014, 7, WOOD_LIGHT, WOOD_LIGHT)
	meshes["storagepit"] = b.commit()

	# drying rack: two A-frames, a bar and hanging strips
	b = _b(319)
	for sx in [-0.045, 0.045]:
		b.pole(Vector3(sx, 0, -0.02), Vector3(sx, 0.07, 0), 0.0035, WOOD)
		b.pole(Vector3(sx, 0, 0.02), Vector3(sx, 0.07, 0), 0.0035, WOOD)
	b.pole(Vector3(-0.05, 0.068, 0), Vector3(0.05, 0.068, 0), 0.003, WOOD)
	for i in 6:
		var x := -0.035 + i * 0.014
		b.box(Vector3(x, 0.05, 0), Vector3(0.007, 0.032, 0.003), Color("#9c4a32") if i % 2 == 0 else Color("#b8916a"))
	meshes["dryingrack"] = b.commit()

	# granary on stilts
	b = _b(321)
	for i in 4:
		var a := TAU * i / 4.0 + PI / 4.0
		b.pole(Vector3(cos(a) * 0.03, 0, sin(a) * 0.03), Vector3(cos(a) * 0.03, 0.035, sin(a) * 0.03), 0.004, WOOD)
	b.prism(Vector3(0, 0.035, 0), 0.042, 0.042, 0.045, 8, DAUB, DAUB)
	b.prism(Vector3(0, 0.077, 0), 0.055, 0.0, 0.06, 8, THATCH, THATCH)
	meshes["granary"] = b.commit()

	# kiln: domed clay with a chimney
	b = _b(323)
	b.blob(Vector3(0, 0.0, 0), Vector3(0.05, 0.05, 0.05), 8, 4, Color("#a8704a"), 0.06, Color("#8a5a3a"))
	b.prism(Vector3(0, 0.04, 0), 0.012, 0.01, 0.035, 6, Color("#8a5a3a"), Color("#2a2119"))
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	b.box(Vector3(0, 0.012, 0.047), Vector3(0.02, 0.018, 0.004), Color("#ff6a20"))
	meshes["kiln"] = b.commit()

	# quern: slab and a round upper stone
	b = _b(325)
	b.prism(Vector3.ZERO, 0.03, 0.028, 0.01, 6, STONE_DARK, STONE)
	b.prism(Vector3(0, 0.01, 0), 0.018, 0.017, 0.01, 8, STONE, STONE.lightened(0.15))
	meshes["quern"] = b.commit()

	# thorn hedge segment (along local X, ~0.12 long)
	b = _b(327)
	b.tag = M7MeshBuilder.TAG_FOLIAGE
	for i in 4:
		var x := -0.05 + i * 0.033 + 0.006 * b.rnds()
		b.blob(Vector3(x, 0.018, 0.004 * b.rnds()), Vector3(0.022, 0.022, 0.018), 5, 3, Color("#5d6234"), 0.5, Color("#4a4a2a"))
	b.tag = M7MeshBuilder.TAG_PLAIN
	for i in 3:
		var x := -0.045 + i * 0.045
		b.pole(Vector3(x, 0, 0), Vector3(x + 0.004, 0.045, 0.003), 0.003, WOOD)
	meshes["thorn"] = b.commit()

	# palisade segment (pointed stakes)
	b = _b(329)
	for i in 7:
		var x := -0.054 + i * 0.018
		var h := 0.075 + 0.012 * b.rnd()
		b.prism(Vector3(x, 0, 0), 0.0075, 0.0075, h, 5, WOOD_LIGHT, WOOD_LIGHT)
		b.prism(Vector3(x, h, 0), 0.0075, 0.0, 0.016, 5, WOOD_LIGHT.lightened(0.1), WOOD)
	b.pole(Vector3(-0.06, 0.05, 0.008), Vector3(0.06, 0.05, 0.008), 0.004, WOOD)
	meshes["palisade"] = b.commit()

	# lookout tower with platform and a small roof
	b = _b(331)
	var th := 0.24
	for i in 4:
		var a := TAU * i / 4.0 + PI / 4.0
		b.pole(Vector3(cos(a) * 0.04, 0, sin(a) * 0.04), Vector3(cos(a) * 0.026, th, sin(a) * 0.026), 0.0045, WOOD)
	for yy in [0.08, 0.16]:
		for i in 4:
			var a0 := TAU * i / 4.0 + PI / 4.0
			var a1 := TAU * (i + 1) / 4.0 + PI / 4.0
			var rr: float = 0.04 - 0.014 * yy / th
			b.pole(Vector3(cos(a0) * rr, yy, sin(a0) * rr), Vector3(cos(a1) * rr, yy + 0.04, sin(a1) * rr), 0.0028, WOOD)
	b.box(Vector3(0, th, 0), Vector3(0.07, 0.008, 0.07), WOOD_LIGHT)
	for i in 4:
		var a := TAU * i / 4.0 + PI / 4.0
		b.pole(Vector3(cos(a) * 0.03, th, sin(a) * 0.03), Vector3(cos(a) * 0.03, th + 0.04, sin(a) * 0.03), 0.003, WOOD)
	b.prism(Vector3(0, th + 0.04, 0), 0.058, 0.0, 0.045, 4, THATCH, THATCH, PI / 4.0)
	meshes["lookout"] = b.commit()

	# player Beacon: a carved hardwood marker topped with a civ-coloured pennant.
	b = _b(332)
	b.prism(Vector3(0, 0, 0), 0.025, 0.022, 0.34, 5, WOOD, WOOD_LIGHT)
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.34, 0), 0.052, 0.0, 0.11, 5, Color.WHITE, Color.WHITE)
	meshes["beacon"] = b.commit()

	# weir: a V of stakes (local +Z downstream)
	b = _b(333)
	for i in 9:
		var t := float(i) / 8.0
		var x := (t - 0.5) * 0.3
		var z := absf(x) * 0.5
		b.prism(Vector3(x, -0.04, z), 0.0055, 0.005, 0.07, 4, WOOD, WOOD_LIGHT)
	b.pole(Vector3(-0.15, 0.015, 0.075), Vector3(0, 0.015, 0.0), 0.003, WOOD_LIGHT)
	b.pole(Vector3(0.15, 0.015, 0.075), Vector3(0, 0.015, 0.0), 0.003, WOOD_LIGHT)
	meshes["weir"] = b.commit()

	# fence segment (pasture), along local X, 0.12 long
	b = _b(335)
	for x in [-0.06, 0.0, 0.06]:
		b.prism(Vector3(x, 0, 0), 0.004, 0.004, 0.04, 4, WOOD, WOOD)
	b.pole(Vector3(-0.065, 0.032, 0), Vector3(0.065, 0.032, 0), 0.0026, WOOD_LIGHT)
	b.pole(Vector3(-0.065, 0.017, 0), Vector3(0.065, 0.017, 0), 0.0026, WOOD_LIGHT)
	meshes["fence"] = b.commit()

	# field: soil hex with crop ridges (local, pointy-top hex, radius 0.9 of the tile)
	b = _b(337)
	var fr := M7WorldData.R * 0.84
	var hexp := []
	for i in 6:
		hexp.append(M7WorldData.corner(i) * 0.84)
	for i in 6:
		var p0: Vector2 = hexp[i]
		var p1: Vector2 = hexp[(i + 1) % 6]
		b.tri(Vector3(0, 0.004, 0), Vector3(p0.x, 0.004, p0.y), Vector3(p1.x, 0.004, p1.y), SOIL, Vector3(0, -1, 0))
	b.tag = M7MeshBuilder.TAG_CROP
	var rows := 15
	for r in rows:
		var z := -fr + (r + 0.5) * (2.0 * fr / rows)
		var hw := _hex_half_width(z, fr)
		if hw < 0.04:
			continue
		hw -= 0.015
		var hh := 0.014
		var wdt := 0.011
		var a0 := Vector3(-hw, 0.004, z - wdt)
		var a1 := Vector3(hw, 0.004, z - wdt)
		var c0 := Vector3(-hw, 0.004 + hh, z)
		var c1 := Vector3(hw, 0.004 + hh, z)
		var d0 := Vector3(-hw, 0.004, z + wdt)
		var d1 := Vector3(hw, 0.004, z + wdt)
		b.quad(a0, a1, c1, c0, SOIL.lightened(0.05), Vector3(0, 0, z + 0.05))
		b.quad(d0, d1, c1, c0, SOIL.darkened(0.05), Vector3(0, 0, z - 0.05))
		b.tri(a0, c0, d0, SOIL, Vector3(0.0, 0.0, z))
		b.tri(a1, c1, d1, SOIL, Vector3(0.0, 0.0, z))
	meshes["field"] = b.commit()

	# camp windbreak (small hide shelter)
	b = _b(339)
	b.prism(Vector3.ZERO, 0.045, 0.01, 0.08, 6, HIDE_DARK, HIDE_DARK)
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.03, 0), 0.0355, 0.03, 0.012, 6, Color.WHITE, Color.WHITE)
	meshes["camp"] = b.commit()

	# banners with emblems (one mesh per civ slot)
	for civ in 8:
		meshes["banner%d" % civ] = _banner(civ, 0.24)
		meshes["banner_small%d" % civ] = _banner(civ, 0.14)

static func _hex_half_width(z: float, r: float) -> float:
	var az := absf(z)
	if az <= r * 0.5:
		return r * 0.8660254
	if az >= r:
		return 0.0
	return r * 0.8660254 * (r - az) / (r * 0.5)

func _banner(civ: int, h: float) -> ArrayMesh:
	var b := _b(400 + civ)
	b.prism(Vector3.ZERO, 0.0055, 0.004, h, 5, WOOD, WOOD)
	b.blob(Vector3(0, h + 0.004, 0), Vector3(0.007, 0.007, 0.007), 5, 2, Color("#d8b25a"))
	var fw := h * 0.48
	var fh := h * 0.3
	var y1 := h - 0.008
	var y0 := y1 - fh
	# flag in civ colour, slightly waved: 3 vertical panels
	b.tag = M7MeshBuilder.TAG_CIV
	var xs := [0.004, fw * 0.33, fw * 0.66, fw]
	var zs := [0.0, 0.008, -0.004, 0.006]
	for i in 3:
		var p0 := Vector3(xs[i], y0, zs[i])
		var p1 := Vector3(xs[i + 1], y0, zs[i + 1])
		var p2 := Vector3(xs[i + 1], y1, zs[i + 1])
		var p3 := Vector3(xs[i], y1, zs[i])
		b.quad(p0, p1, p2, p3, Color.WHITE, Vector3(xs[i] + 0.01, (y0 + y1) * 0.5, -0.05))
	# swallow tail
	b.tri(Vector3(fw, y0, zs[3]), Vector3(fw + fh * 0.35, y0 - fh * 0.15, zs[3]), Vector3(fw, y0 + fh * 0.5, zs[3]), Color.WHITE, Vector3(fw, y0, -0.05))
	# emblem on both faces
	b.tag = M7MeshBuilder.TAG_PLAIN
	var cx := fw * 0.5
	var cy := (y0 + y1) * 0.5
	var s := fh * 0.42
	for poly in M7Emblems.polygons(civ):
		var idx := Geometry2D.triangulate_polygon(poly)
		for side in [-1.0, 1.0]:
			for k in range(0, idx.size(), 3):
				var pts := []
				for j in 3:
					var q: Vector2 = poly[idx[k + j]]
					var x := cx + q.x * s
					var zf: float = _flag_z(x, xs, zs) + side * 0.0025
					pts.append(Vector3(x, cy + q.y * s, zf))
				b.tri(pts[0], pts[1], pts[2], CREAM, Vector3(cx, cy, -side * 0.05))
	return b.commit()

static func _flag_z(x: float, xs: Array, zs: Array) -> float:
	for i in 3:
		if x <= xs[i + 1]:
			var t: float = (x - xs[i]) / (xs[i + 1] - xs[i])
			return lerpf(zs[i], zs[i + 1], clampf(t, 0.0, 1.0))
	return zs[3]

# ------------------------------------------------------------------ people

## Adult figure, ~0.1 tall, facing +Z. tool: "", spear, basket, axe, hoe, pole, hammer, stick
## Tool-head colour by tier: 0 flaked stone (the base colour), 1 ground and polished stone, 2 copper, 3 bronze.
const TOOL_TIER_COLORS := [Color("#6c675f"), Color("#a79f8e"), Color("#b4673a"), Color("#c9973c")]
## Tool tiers a figure can carry: the sim's method rung (0 .. 6+) folded into four looks.
const TOOL_TIERS := 4

static func tool_tier_for_rung(rung: int) -> int:
	if rung <= 1:
		return 0
	if rung <= 3:
		return 1
	if rung <= 5:
		return 2
	return 3

func _person(seed: int, tool: String, stoop: float = 0.0, elder: bool = false, tier: int = 0) -> ArrayMesh:
	var b := _b(seed)
	# heads grow a little with each tier (a longer blade, a heavier head) so the step reads at a distance
	var grow := 1.0 + 0.18 * tier
	var head_color: Color = TOOL_TIER_COLORS[tier]
	var legc := Color("#4a3a2a")
	# legs
	b.prism(Vector3(-0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, legc, legc)
	b.prism(Vector3(0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, legc, legc)
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis(Vector3.RIGHT, stoop), Vector3(0, 0.034, 0))
	# torso (hide tunic, toned per instance)
	b.tag = M7MeshBuilder.TAG_CLOTH
	b.prism(Vector3(0, 0, 0), 0.018, 0.015, 0.036, 7, Color("#8a6a48"), Color("#8a6a48"))
	# civ mantle over the shoulders: the main identity read from above
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.02, 0), 0.0215, 0.009, 0.022, 7, Color.WHITE, Color.WHITE)
	# sash diagonal band
	b.prism(Vector3(0, 0.002, 0), 0.0188, 0.0185, 0.007, 7, Color.WHITE, Color.WHITE)
	# arms
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.pole(Vector3(-0.02, 0.03, 0), Vector3(-0.024, 0.006, 0.006), 0.0045, SKIN)
	b.pole(Vector3(0.02, 0.03, 0), Vector3(0.026, 0.008, 0.01), 0.0045, SKIN)
	# head and hair
	b.blob(Vector3(0, 0.05, 0), Vector3(0.0125, 0.013, 0.0125), 7, 4, SKIN, 0.0)
	var hc := Color("#cfcac0") if elder else HAIR
	b.blob(Vector3(0, 0.055, -0.003), Vector3(0.0128, 0.009, 0.012), 7, 3, hc, 0.0)
	# tools in the right hand (x+)
	var hand := Vector3(0.026, 0.008, 0.012)
	match tool:
		"spear":
			b.pole(hand + Vector3(0, -0.035, -0.01), hand + Vector3(0.004, 0.07, 0.012), 0.0028, WOOD_LIGHT)
			b.prism(hand + Vector3(0.004, 0.07, 0.012), 0.005, 0.0, 0.016 * grow, 4, head_color, head_color)
		"basket":
			b.prism(Vector3(-0.026, -0.012, 0.004), 0.011, 0.014, 0.016, 6, Color("#b08a4e"), Color("#5a7a2c"))
		"axe":
			b.pole(hand, hand + Vector3(0.006, 0.045, 0.014), 0.0026, WOOD_LIGHT)
			b.box(hand + Vector3(0.012, 0.043, 0.014), Vector3(0.014 * grow, 0.01 * grow, 0.005), head_color)
		"hoe":
			b.pole(hand + Vector3(0, -0.03, -0.006), hand + Vector3(0.002, 0.05, 0.01), 0.0026, WOOD_LIGHT)
			b.box(hand + Vector3(0.002, -0.032, 0.0), Vector3(0.006, 0.006, 0.016 * grow), head_color)
		"pole":
			b.pole(hand, hand + Vector3(0.01, 0.06, 0.07), 0.0022, WOOD_LIGHT)
		"hammer":
			b.pole(hand, hand + Vector3(0.0, 0.03, 0.012), 0.0026, WOOD_LIGHT)
			b.box(hand + Vector3(0.0, 0.032, 0.012), Vector3(0.014 * grow, 0.01 * grow, 0.01), STONE if tier == 0 else head_color)
		"bundle":
			b.box(Vector3(0, 0.02, -0.022), Vector3(0.026, 0.026, 0.016), Color("#a5814f"))
	b.xf = save
	if tool == "stick":
		b.pole(Vector3(0.03, 0, 0.02), Vector3(0.026, 0.06, 0.016), 0.0026, WOOD_LIGHT)
	return b.commit()

## Loads carried home on a person's back or in a hand, in the person's local frame (+Y up, the back is -Z). Index = the sim's
## CarryKind (1 plants, 2 meat, 3 fish, 4 wood, 5 stone, 6 grain, 7 goods, 8 loot).
const CARRY_NAMES := ["", "plants", "meat", "fish", "wood", "stone", "grain", "goods", "loot"]

func _carry_meshes() -> void:
	for kind in range(1, CARRY_NAMES.size()):
		var b := _b(600 + kind)
		match kind:
			1:   # a pack basket of roots and greens
				b.prism(Vector3(0, 0.02, -0.027), 0.012, 0.015, 0.022, 6, Color("#b08a4e"), Color("#5a7a2c"))
				b.blob(Vector3(-0.004, 0.047, -0.027), Vector3(0.008, 0.006, 0.008), 5, 3, Color("#6f9a34"))
				b.blob(Vector3(0.006, 0.045, -0.025), Vector3(0.006, 0.005, 0.006), 5, 3, Color("#d6ae43"))
			2:   # a carcass slung over the shoulders
				b.blob(Vector3(0.0, 0.052, -0.022), Vector3(0.024, 0.011, 0.013), 6, 3, Color("#a34431"), 0.0, Color("#7a2f22"))
				b.pole(Vector3(-0.02, 0.046, -0.026), Vector3(-0.034, 0.03, -0.03), 0.0032, Color("#8f7354"))
				b.pole(Vector3(0.02, 0.046, -0.026), Vector3(0.034, 0.03, -0.03), 0.0032, Color("#8f7354"))
			3:   # fish on a string from the left hand
				for i in 3:
					var x := -0.028 - 0.004 * i
					b.blob(Vector3(x, 0.002 - 0.011 * i, 0.008), Vector3(0.004, 0.009, 0.0026), 5, 3, Color("#7fb0b8"), 0.0, Color("#d8e6e4"))
				b.pole(Vector3(-0.026, 0.008, 0.006), Vector3(-0.034, -0.03, 0.008), 0.0012, Color("#6b5a40"))
			4:   # sticks and branches bundled upright
				b.pole(Vector3(-0.012, 0.014, -0.026), Vector3(-0.006, 0.086, -0.03), 0.0032, WOOD_LIGHT)
				b.pole(Vector3(0.0, 0.012, -0.027), Vector3(0.01, 0.092, -0.026), 0.0032, Color("#6e5238"))
				b.pole(Vector3(0.012, 0.014, -0.026), Vector3(0.02, 0.08, -0.032), 0.0032, WOOD_LIGHT)
				b.pole(Vector3(-0.02, 0.016, -0.025), Vector3(-0.022, 0.07, -0.02), 0.0028, Color("#6e5238"))
			5:   # a stone on the shoulder
				b.blob(Vector3(0.0, 0.034, -0.026), Vector3(0.016, 0.013, 0.013), 6, 3, STONE, 0.1, STONE_DARK)
				b.blob(Vector3(0.012, 0.024, -0.024), Vector3(0.009, 0.007, 0.008), 5, 3, STONE_DARK, 0.1)
			6:   # a sheaf of grain
				b.prism(Vector3(0, 0.012, -0.026), 0.006, 0.017, 0.05, 6, Color("#d6ae43"), Color("#e4c25a"))
				b.pole(Vector3(-0.017, 0.032, -0.026), Vector3(0.017, 0.032, -0.026), 0.0022, Color("#8a6a3a"))
			7:   # a wrapped bundle for a gift or barter
				b.box(Vector3(0, 0.036, -0.028), Vector3(0.03, 0.03, 0.02), Color("#bf8c58"), Color("#d2a26e"))
				b.box(Vector3(0, 0.036, -0.0285), Vector3(0.006, 0.032, 0.021), Color("#7a5a3a"))
			8:   # a loot sack
				b.blob(Vector3(0.0, 0.034, -0.028), Vector3(0.017, 0.018, 0.014), 6, 4, Color("#8a6a45"), 0.06, Color("#6e5238"))
				b.pole(Vector3(-0.006, 0.05, -0.028), Vector3(0.006, 0.05, -0.028), 0.0026, Color("#4a3a2a"))
		meshes["person_carry_" + CARRY_NAMES[kind]] = b.commit()

func _people() -> void:
	_carry_meshes()
	for t in ["", "spear", "basket", "axe", "hoe", "pole", "hammer", "bundle"]:
		meshes["person_" + (t if t != "" else "plain")] = _person(500 + t.length(), t)
		if t in ["spear", "axe", "hoe", "hammer"]:
			for tier in range(1, TOOL_TIERS):
				meshes["person_%s_t%d" % [t, tier]] = _person(500 + t.length(), t, 0.0, false, tier)
	meshes["person_elder"] = _person(551, "stick", 0.3, true)
	# child: scaled-down proportions with a larger head
	var b := _b(561)
	var legc := Color("#4a3a2a")
	b.prism(Vector3(-0.005, 0, 0), 0.0045, 0.005, 0.02, 5, legc, legc)
	b.prism(Vector3(0.005, 0, 0), 0.0045, 0.005, 0.02, 5, legc, legc)
	b.tag = M7MeshBuilder.TAG_CLOTH
	b.prism(Vector3(0, 0.019, 0), 0.012, 0.0105, 0.024, 7, Color("#9a7a55"), Color("#9a7a55"))
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.032, 0), 0.0135, 0.007, 0.012, 7, Color.WHITE, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.blob(Vector3(0, 0.05, 0), Vector3(0.011, 0.0115, 0.011), 7, 4, SKIN, 0.0)
	b.blob(Vector3(0, 0.054, -0.003), Vector3(0.0112, 0.008, 0.0105), 7, 3, HAIR, 0.0)
	meshes["person_child"] = b.commit()
	# lying figure on a hide mat (convalescing)
	b = _b(571)
	b.box(Vector3(0, 0.003, 0), Vector3(0.05, 0.004, 0.1), HIDE_DARK)
	b.tag = M7MeshBuilder.TAG_CLOTH
	b.blob(Vector3(0, 0.012, 0.0), Vector3(0.016, 0.01, 0.04), 6, 3, Color("#8a6a48"))
	b.tag = M7MeshBuilder.TAG_CIV
	b.blob(Vector3(0, 0.016, -0.012), Vector3(0.018, 0.009, 0.022), 6, 3, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.blob(Vector3(0, 0.013, -0.045), Vector3(0.012, 0.011, 0.012), 6, 3, SKIN)
	meshes["person_lying"] = b.commit()
	# soft contact shadow disc under figures (dark, flat)
	b = _b(573)
	for i in 8:
		var a0 := TAU * i / 8.0
		var a1 := TAU * (i + 1) / 8.0
		b.tri(Vector3(0, 0.002, 0), Vector3(cos(a0) * 0.022, 0.002, sin(a0) * 0.022), Vector3(cos(a1) * 0.022, 0.002, sin(a1) * 0.022), Color("#20180f"), Vector3(0, -1, 0))
	meshes["shadow_disc"] = b.commit()
	# At map scale the kit details and contact shadows are sub-pixel; keep each person
	# visible as a civ-coloured figure with a head using only a few polygons.
	b = _b(581)
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.004, 0), 0.016, 0.011, 0.04, 4, Color.WHITE, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.blob(Vector3(0, 0.051, 0), Vector3(0.01, 0.011, 0.01), 5, 2, SKIN)
	meshes["person_far"] = b.commit()

# ------------------------------------------------------------------ animals (facing +X, body in civ/species colour)

func _animals() -> void:
	# heavy: aurochs, bison, boar, horse
	var b := _b(601)
	var legc := Color("#3a2e24")
	for p in [Vector3(-0.03, 0, -0.012), Vector3(-0.03, 0, 0.012), Vector3(0.028, 0, -0.012), Vector3(0.028, 0, 0.012)]:
		b.prism(p, 0.006, 0.007, 0.04, 4, legc, legc)
	b.tag = M7MeshBuilder.TAG_CIV
	b.blob(Vector3(0, 0.052, 0), Vector3(0.05, 0.024, 0.022), 7, 4, Color.WHITE, 0.08)
	b.blob(Vector3(0.022, 0.066, 0), Vector3(0.025, 0.018, 0.019), 6, 3, Color.WHITE, 0.08)  # shoulder hump
	b.blob(Vector3(0.058, 0.05, 0), Vector3(0.016, 0.013, 0.012), 6, 3, Color.WHITE, 0.05)    # head
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.pole(Vector3(0.06, 0.06, -0.008), Vector3(0.066, 0.072, -0.02), 0.0025, CREAM)
	b.pole(Vector3(0.06, 0.06, 0.008), Vector3(0.066, 0.072, 0.02), 0.0025, CREAM)
	meshes["animal_heavy"] = b.commit()
	# slender: deer, reindeer, gazelle, goat/sheep, camel/llama
	b = _b(611)
	for p in [Vector3(-0.024, 0, -0.008), Vector3(-0.024, 0, 0.008), Vector3(0.022, 0, -0.008), Vector3(0.022, 0, 0.008)]:
		b.prism(p, 0.0038, 0.0045, 0.048, 4, legc, legc)
	b.tag = M7MeshBuilder.TAG_CIV
	b.blob(Vector3(0, 0.056, 0), Vector3(0.036, 0.016, 0.014), 7, 4, Color.WHITE, 0.05)
	b.pole(Vector3(0.026, 0.06, 0), Vector3(0.04, 0.085, 0), 0.006, Color.WHITE)
	b.blob(Vector3(0.046, 0.088, 0), Vector3(0.013, 0.008, 0.008), 6, 3, Color.WHITE, 0.0)
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.pole(Vector3(0.042, 0.094, -0.004), Vector3(0.036, 0.115, -0.012), 0.0018, Color("#d9cdb3"))
	b.pole(Vector3(0.042, 0.094, 0.004), Vector3(0.036, 0.115, 0.012), 0.0018, Color("#d9cdb3"))
	b.blob(Vector3(-0.036, 0.058, 0), Vector3(0.004, 0.004, 0.004), 4, 2, CREAM)
	meshes["animal_slender"] = b.commit()
	# woolly (sheep/goat)
	b = _b(621)
	for p in [Vector3(-0.016, 0, -0.008), Vector3(-0.016, 0, 0.008), Vector3(0.016, 0, -0.008), Vector3(0.016, 0, 0.008)]:
		b.prism(p, 0.0035, 0.004, 0.026, 4, legc, legc)
	b.tag = M7MeshBuilder.TAG_CIV
	b.blob(Vector3(0, 0.036, 0), Vector3(0.03, 0.019, 0.019), 7, 4, Color.WHITE, 0.12)
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.blob(Vector3(0.032, 0.042, 0), Vector3(0.011, 0.009, 0.008), 6, 3, Color("#3a3029"), 0.0)
	meshes["animal_woolly"] = b.commit()
	# small game
	b = _b(631)
	b.tag = M7MeshBuilder.TAG_CIV
	b.blob(Vector3(0, 0.012, 0), Vector3(0.016, 0.011, 0.01), 6, 3, Color.WHITE, 0.05)
	b.blob(Vector3(0.015, 0.02, 0), Vector3(0.007, 0.007, 0.006), 5, 3, Color.WHITE, 0.0)
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.pole(Vector3(0.016, 0.025, -0.003), Vector3(0.012, 0.04, -0.004), 0.002, Color("#b9a68a"))
	b.pole(Vector3(0.016, 0.025, 0.003), Vector3(0.012, 0.04, 0.004), 0.002, Color("#b9a68a"))
	meshes["animal_small"] = b.commit()


# ------------------------------------------------------------------ dogs (HUS-01, TRN-02; see dog_rig.gd)
# Facing +X like the animals, about 0.05 long. Coats: 0 tan, 1 dark, 2 pale. Poses: trot_a / trot_b (legs swapped, played by
# distance walked), stand, sit, lie. A collar in the civ colour keeps them readable and says whose they are.

const DOG_COATS := [
	[Color("#a56a2c"), Color("#d8b98a"), Color("#5a3a1e")],    # tan: coat, belly and muzzle, ears
	[Color("#3d342d"), Color("#6a5d52"), Color("#241d18")],    # dark
	[Color("#b9a67c"), Color("#e2d6b8"), Color("#8a7450")],    # pale
]

func _dog(seed: int, coat: int, pose: String) -> ArrayMesh:
	var b := _b(seed)
	var c: Color = DOG_COATS[coat][0]
	var belly: Color = DOG_COATS[coat][1]
	var dark: Color = DOG_COATS[coat][2]
	b.tag = M7MeshBuilder.TAG_PLAIN
	match pose:
		"sit":
			# haunches on the ground, chest and head lifted, forelegs straight
			b.blob(Vector3(-0.012, 0.011, 0.0), Vector3(0.016, 0.012, 0.012), 6, 3, c, 0.05)
			b.blob(Vector3(-0.002, 0.022, 0.0), Vector3(0.012, 0.016, 0.0105), 6, 3, c, 0.05)
			b.blob(Vector3(0.0, 0.018, 0.0), Vector3(0.0075, 0.012, 0.0085), 6, 3, belly, 0.0)
			b.pole(Vector3(0.008, 0.026, -0.005), Vector3(0.011, 0.0, -0.005), 0.0028, c)
			b.pole(Vector3(0.008, 0.026, 0.005), Vector3(0.011, 0.0, 0.005), 0.0028, c)
			b.blob(Vector3(-0.024, 0.0035, -0.0105), Vector3(0.012, 0.0035, 0.0045), 5, 2, c, 0.0)
			b.blob(Vector3(-0.024, 0.0035, 0.0105), Vector3(0.012, 0.0035, 0.0045), 5, 2, c, 0.0)
			b.pole(Vector3(-0.026, 0.006, 0.0), Vector3(-0.043, 0.004, 0.004), 0.0028, c)
			_dog_head(b, Vector3(0.006, 0.039, 0.0), c, belly, dark, 0.0)
			_dog_collar(b, Vector3(0.002, 0.0325, 0.0), 0.0075)
		"lie":
			b.blob(Vector3(0.0, 0.0095, 0.0), Vector3(0.022, 0.0095, 0.0115), 6, 3, c, 0.05)
			b.blob(Vector3(0.003, 0.0035, 0.0), Vector3(0.017, 0.004, 0.0095), 5, 2, belly, 0.0)
			b.pole(Vector3(0.015, 0.003, -0.006), Vector3(0.036, 0.0025, -0.006), 0.0028, c)
			b.pole(Vector3(0.015, 0.003, 0.006), Vector3(0.036, 0.0025, 0.006), 0.0028, c)
			b.pole(Vector3(-0.021, 0.008, 0.004), Vector3(-0.035, 0.004, 0.012), 0.003, c)
			_dog_head(b, Vector3(0.027, 0.0165, 0.0), c, belly, dark, 0.35)
			_dog_collar(b, Vector3(0.0185, 0.0125, 0.0), 0.0072)
		_:
			var swing := 0.0
			if pose == "trot_a":
				swing = 0.009
			elif pose == "trot_b":
				swing = -0.009
			# a diagonal pair swings together: front left with back right
			for leg in [[0.0145, -0.0055, 1.0], [0.0145, 0.0055, -1.0], [-0.0145, -0.0055, -1.0], [-0.0145, 0.0055, 1.0]]:
				var dx: float = float(leg[2]) * swing
				var lift := 0.0035 if (dx > 0.0) else 0.0
				b.pole(Vector3(leg[0], 0.0235, leg[1]), Vector3(leg[0] + dx, lift, leg[1]), 0.0028, c)
			b.blob(Vector3(0.0, 0.0275, 0.0), Vector3(0.0235, 0.0115, 0.0105), 7, 3, c, 0.05)
			b.blob(Vector3(0.0, 0.0225, 0.0), Vector3(0.0185, 0.0075, 0.0085), 6, 3, belly, 0.0)
			b.blob(Vector3(0.0115, 0.0295, 0.0), Vector3(0.0105, 0.0115, 0.0108), 6, 3, c, 0.04)    # deep chest
			b.pole(Vector3(-0.021, 0.0285, 0.0), Vector3(-0.0385, 0.0415 if pose != "stand" else 0.037, 0.0), 0.0026, c)
			b.blob(Vector3(-0.0395, 0.0425 if pose != "stand" else 0.038, 0.0), Vector3(0.0035, 0.0025, 0.0028), 4, 2, dark, 0.0)
			_dog_head(b, Vector3(0.0275, 0.0385, 0.0), c, belly, dark, 0.0)
			_dog_collar(b, Vector3(0.0205, 0.0345, 0.0), 0.0078)
	return b.commit()

func _dog_head(b: M7MeshBuilder, p: Vector3, c: Color, belly: Color, dark: Color, droop: float) -> void:
	b.blob(p, Vector3(0.0085, 0.0078, 0.0075), 6, 3, c, 0.03)
	b.blob(p + Vector3(0.0085, -0.0025 - droop * 0.004, 0.0), Vector3(0.0075, 0.0042, 0.0042), 5, 2, belly, 0.0)    # muzzle
	b.blob(p + Vector3(0.0155, -0.0016 - droop * 0.005, 0.0), Vector3(0.0026, 0.0024, 0.0026), 4, 2, dark, 0.0)      # nose
	b.tri(p + Vector3(-0.002, 0.005, -0.0045), p + Vector3(-0.005, 0.0105 - droop * 0.007, -0.0085), p + Vector3(0.0025, 0.0075, -0.003), dark, p)
	b.tri(p + Vector3(-0.002, 0.005, 0.0045), p + Vector3(-0.005, 0.0105 - droop * 0.007, 0.0085), p + Vector3(0.0025, 0.0075, 0.003), dark, p)

func _dog_collar(b: M7MeshBuilder, p: Vector3, r: float) -> void:
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(p + Vector3(0, -0.0016, 0), r, r, 0.0034, 6, Color.WHITE, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN

func _dogs() -> void:
	for coat in 3:
		for pose in ["trot_a", "trot_b", "stand", "sit", "lie"]:
			meshes["dog_%d_%s" % [coat, pose]] = _dog(900 + coat * 10 + ["trot_a", "trot_b", "stand", "sit", "lie"].find(pose), coat, pose)
	# the travois a dog drags (TRN-02): a strap across the chest, two poles trailing to the ground behind, a lashed load
	var b := _b(940)
	var strap := Color("#7a5a3a")
	b.pole(Vector3(0.0, 0.0345, -0.0075), Vector3(0.0, 0.0345, 0.0075), 0.0022, strap)
	b.pole(Vector3(0.002, 0.034, -0.0075), Vector3(-0.012, 0.0275, -0.0125), 0.0018, strap)
	b.pole(Vector3(0.002, 0.034, 0.0075), Vector3(-0.012, 0.0275, 0.0125), 0.0018, strap)
	b.pole(Vector3(-0.012, 0.0275, -0.0125), Vector3(-0.115, 0.0015, -0.0165), 0.0026, WOOD_LIGHT)
	b.pole(Vector3(-0.012, 0.0275, 0.0125), Vector3(-0.115, 0.0015, 0.0165), 0.0026, WOOD_LIGHT)
	b.pole(Vector3(-0.05, 0.0155, -0.0145), Vector3(-0.05, 0.0155, 0.0145), 0.0022, WOOD)
	b.pole(Vector3(-0.085, 0.0075, -0.0158), Vector3(-0.085, 0.0075, 0.0158), 0.0022, WOOD)
	b.box(Vector3(-0.066, 0.0165, 0.0), Vector3(0.05, 0.0085, 0.024), HIDE_DARK, HIDE)
	b.box(Vector3(-0.066, 0.0225, 0.0), Vector3(0.036, 0.006, 0.018), Color("#a98a5e"), Color("#c0a374"))
	b.pole(Vector3(-0.055, 0.0165, -0.0125), Vector3(-0.055, 0.0275, 0.0125), 0.0012, strap)
	b.pole(Vector3(-0.077, 0.0165, -0.0125), Vector3(-0.077, 0.0275, 0.0125), 0.0012, strap)
	meshes["dog_travois"] = b.commit()


# ------------------------------------------------------------------ village decor (see game/Scripts/DecorTable.cs)

## Index = the decor kind the host sends; mesh name = "decor_" + name.
const DECOR_NAMES := ["totem", "cairn", "hideframe", "pots", "loom", "shrine", "signal", "charcoal", "furnace",
	"sledge", "palisade", "spearrack", "grave", "council", "quarry", "skypole", "bricks", "baskets", "woodwork"]

func _decor() -> void:
	var b := _b(700)
	# totem: a carved post, a civ-coloured band, a beaked head and spread wings
	b.pole(Vector3(0, 0, 0), Vector3(0, 0.12, 0), 0.009, WOOD)
	b.box(Vector3(0, 0.032, 0), Vector3(0.03, 0.028, 0.03), Color("#a6603a"))
	b.tag = M7MeshBuilder.TAG_CIV
	b.box(Vector3(0, 0.066, 0), Vector3(0.034, 0.012, 0.034), Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.box(Vector3(0, 0.088, 0), Vector3(0.07, 0.008, 0.012), WOOD_LIGHT)
	b.box(Vector3(0, 0.106, 0.004), Vector3(0.026, 0.022, 0.028), Color("#d8c08a"))
	b.box(Vector3(0, 0.102, 0.022), Vector3(0.01, 0.01, 0.016), Color("#c9973c"))
	meshes["decor_totem"] = b.commit()

	# cairn: three stacked stones
	b = _b(701)
	b.blob(Vector3(0, 0.008, 0), Vector3(0.032, 0.014, 0.03), 6, 3, STONE, 0.1, STONE_DARK)
	b.blob(Vector3(0.002, 0.024, 0), Vector3(0.022, 0.011, 0.02), 6, 3, ROCK_HI, 0.1, STONE)
	b.blob(Vector3(-0.001, 0.036, 0.001), Vector3(0.013, 0.009, 0.013), 5, 3, STONE, 0.1)
	meshes["decor_cairn"] = b.commit()

	# hide frame: a stretched hide laced into a pole frame
	b = _b(703)
	b.pole(Vector3(-0.034, 0, 0), Vector3(-0.034, 0.07, 0), 0.0036, WOOD)
	b.pole(Vector3(0.034, 0, 0), Vector3(0.034, 0.07, 0), 0.0036, WOOD)
	b.pole(Vector3(-0.036, 0.067, 0), Vector3(0.036, 0.067, 0), 0.0032, WOOD)
	b.pole(Vector3(-0.036, 0.012, 0), Vector3(0.036, 0.012, 0), 0.0032, WOOD)
	b.box(Vector3(0, 0.04, 0), Vector3(0.062, 0.05, 0.003), HIDE, HIDE_DARK)
	meshes["decor_hideframe"] = b.commit()

	# pots: three fired clay pots
	b = _b(704)
	for pot in [[Vector3(0, 0.012, 0), 0.015, Color("#b5603c")], [Vector3(0.032, 0.009, 0.012), 0.011, Color("#c27a4a")],
			[Vector3(-0.028, 0.01, 0.016), 0.012, Color("#a4532f")]]:
		var pp: Vector3 = pot[0]
		var pr: float = pot[1]
		b.blob(pp, Vector3(pr, pr * 0.95, pr), 7, 4, pot[2], 0.0, pot[2].darkened(0.2))
		b.prism(pp + Vector3(0, pr * 0.8, 0), pr * 0.6, pr * 0.6, 0.003, 7, Color("#2a1c12"), Color("#2a1c12"))
	meshes["decor_pots"] = b.commit()

	# loom: warp-weighted, a striped cloth growing
	b = _b(705)
	b.pole(Vector3(-0.034, 0, 0), Vector3(-0.034, 0.1, 0), 0.0036, WOOD)
	b.pole(Vector3(0.034, 0, 0), Vector3(0.034, 0.1, 0), 0.0036, WOOD)
	b.pole(Vector3(-0.038, 0.096, 0), Vector3(0.038, 0.096, 0), 0.0032, WOOD_LIGHT)
	for i in 7:
		var x := -0.027 + i * 0.009
		b.box(Vector3(x, 0.072, 0), Vector3(0.006, 0.04, 0.002), Color("#9c4a32") if i % 3 == 0 else (Color("#e4d6b8") if i % 3 == 1 else Color("#3f5a7a")))
		b.box(Vector3(x, 0.036, 0), Vector3(0.0014, 0.032, 0.0014), Color("#cdbd9a"))
		b.blob(Vector3(x, 0.016, 0), Vector3(0.0042, 0.0045, 0.0042), 4, 3, STONE_DARK)
	meshes["decor_loom"] = b.commit()

	# shrine: a stone platform, two uprights and a lintel, an offering lamp
	b = _b(706)
	b.prism(Vector3.ZERO, 0.052, 0.048, 0.01, 6, STONE, STONE.lightened(0.1))
	b.pole(Vector3(-0.026, 0.01, 0), Vector3(-0.026, 0.075, 0), 0.0065, STONE)
	b.pole(Vector3(0.026, 0.01, 0), Vector3(0.026, 0.075, 0), 0.0065, STONE)
	b.box(Vector3(0, 0.08, 0), Vector3(0.07, 0.01, 0.016), ROCK_HI)
	b.blob(Vector3(0, 0.016, 0.012), Vector3(0.008, 0.005, 0.008), 5, 3, Color("#7a5a3a"))
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	b.box(Vector3(0, 0.024, 0.012), Vector3(0.006, 0.008, 0.006), Color("#ffb347"))
	meshes["decor_shrine"] = b.commit()

	# signal fire: a stacked pyre on a stone ring (the flame is added by the builder)
	b = _b(707)
	b.prism(Vector3.ZERO, 0.036, 0.034, 0.008, 7, STONE, STONE)
	b.pole(Vector3(-0.022, 0.008, 0.0), Vector3(0.0, 0.07, 0.0), 0.004, WOOD)
	b.pole(Vector3(0.022, 0.008, 0.004), Vector3(0.0, 0.07, 0.0), 0.004, BARK)
	b.pole(Vector3(0.0, 0.008, -0.022), Vector3(0.0, 0.07, 0.0), 0.004, WOOD_LIGHT)
	b.pole(Vector3(0.004, 0.008, 0.022), Vector3(0.0, 0.07, 0.0), 0.004, BARK)
	meshes["decor_signal"] = b.commit()

	# charcoal mound: earth-covered and smoking
	b = _b(708)
	b.blob(Vector3(0, 0.0, 0), Vector3(0.05, 0.034, 0.05), 8, 4, Color("#2d261f"), 0.08, Color("#1d1814"))
	b.prism(Vector3(0, 0.03, 0), 0.008, 0.005, 0.012, 6, Color("#15110d"), Color("#15110d"))
	b.pole(Vector3(0.05, 0, 0.0), Vector3(0.036, 0.03, 0.0), 0.003, WOOD)
	b.pole(Vector3(-0.05, 0, 0.0), Vector3(-0.036, 0.03, 0.0), 0.003, WOOD)
	meshes["decor_charcoal"] = b.commit()

	# furnace: a clay dome with a bellows pipe, ore and a bar of copper beside it
	b = _b(709)
	b.blob(Vector3(0, 0.0, 0), Vector3(0.032, 0.046, 0.032), 7, 4, Color("#9a5a3a"), 0.05, Color("#7a4a30"))
	b.pole(Vector3(0.018, 0.01, 0.026), Vector3(0.052, 0.006, 0.06), 0.004, Color("#3a2c1f"))
	b.blob(Vector3(-0.046, 0.004, 0.02), Vector3(0.014, 0.008, 0.014), 5, 3, Color("#4f8f6a"), 0.1)
	b.box(Vector3(-0.03, 0.003, 0.05), Vector3(0.022, 0.005, 0.008), Color("#c0683a"))
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	b.box(Vector3(0, 0.012, 0.03), Vector3(0.016, 0.014, 0.004), Color("#ff6a20"))
	meshes["decor_furnace"] = b.commit()

	# sledge: runners and cross boards under a stone block
	b = _b(711)
	b.pole(Vector3(-0.03, 0.004, -0.055), Vector3(-0.03, 0.01, 0.065), 0.004, WOOD)
	b.pole(Vector3(0.03, 0.004, -0.055), Vector3(0.03, 0.01, 0.065), 0.004, WOOD)
	for z in [-0.04, 0.0, 0.04]:
		b.pole(Vector3(-0.036, 0.012, z), Vector3(0.036, 0.012, z), 0.0034, WOOD_LIGHT)
	b.box(Vector3(0, 0.026, 0), Vector3(0.05, 0.022, 0.06), ROCK, ROCK_HI)
	b.pole(Vector3(-0.03, 0.006, -0.055), Vector3(0.0, 0.03, -0.1), 0.0016, Color("#b9a68a"))
	meshes["decor_sledge"] = b.commit()

	# palisade segment (along local X, ~0.12 long): sharpened stakes and a rail
	b = _b(712)
	for i in 6:
		var x := -0.055 + i * 0.022
		b.prism(Vector3(x, 0, 0), 0.0075, 0.0075, 0.082, 5, WOOD if i % 2 == 0 else BARK, WOOD)
		b.prism(Vector3(x, 0.082, 0), 0.0075, 0.0, 0.018, 5, WOOD_LIGHT, WOOD_LIGHT)
	b.pole(Vector3(-0.06, 0.04, 0.007), Vector3(0.06, 0.04, 0.007), 0.0028, WOOD_LIGHT)
	meshes["decor_palisade"] = b.commit()

	# spear rack: a bar between two posts with spears leaning against it
	b = _b(713)
	b.pole(Vector3(-0.04, 0, 0), Vector3(-0.04, 0.05, 0), 0.0042, WOOD)
	b.pole(Vector3(0.04, 0, 0), Vector3(0.04, 0.05, 0), 0.0042, WOOD)
	b.pole(Vector3(-0.044, 0.046, 0), Vector3(0.044, 0.046, 0), 0.003, WOOD_LIGHT)
	for i in 5:
		var x := -0.03 + i * 0.015
		b.pole(Vector3(x, 0, 0.022), Vector3(x * 0.8, 0.095, 0.002), 0.0022, WOOD_LIGHT)
		b.prism(Vector3(x * 0.8, 0.095, 0.002), 0.004, 0.0, 0.014, 4, STONE_DARK, STONE_DARK)
	meshes["decor_spearrack"] = b.commit()

	# grave: a low mound with a stone at the head and a smear of red ochre
	b = _b(714)
	b.blob(Vector3(0, 0, 0), Vector3(0.04, 0.014, 0.025), 6, 3, EARTH, 0.05, EARTH)
	b.box(Vector3(0, 0.013, 0.0), Vector3(0.02, 0.002, 0.03), Color("#9c3a22"))
	b.prism(Vector3(0, 0, -0.032), 0.009, 0.006, 0.032, 5, STONE, STONE.lightened(0.1))
	meshes["decor_grave"] = b.commit()

	# council circle: log seats round a chief's staff with a civ pennant
	b = _b(715)
	for i in 6:
		var a := TAU * i / 6.0
		b.prism(Vector3(cos(a) * 0.045, 0, sin(a) * 0.045), 0.012, 0.012, 0.014, 6, WOOD_LIGHT, WOOD_LIGHT.lightened(0.12))
	b.pole(Vector3.ZERO, Vector3(0, 0.08, 0), 0.0028, WOOD)
	b.tag = M7MeshBuilder.TAG_CIV
	b.box(Vector3(0.012, 0.072, 0), Vector3(0.024, 0.014, 0.002), Color.WHITE)
	meshes["decor_council"] = b.commit()

	# quarry: cut blocks, rubble and a wedge and pick
	b = _b(716)
	b.box(Vector3(-0.022, 0.012, 0), Vector3(0.04, 0.024, 0.03), STONE, STONE.lightened(0.1))
	b.box(Vector3(0.022, 0.01, 0.012), Vector3(0.03, 0.02, 0.03), STONE_DARK, STONE)
	b.box(Vector3(-0.004, 0.034, 0.002), Vector3(0.036, 0.02, 0.03), ROCK_HI, ROCK_HI.lightened(0.1))
	b.blob(Vector3(0.04, 0.004, -0.03), Vector3(0.012, 0.007, 0.012), 5, 3, ROCK, 0.1)
	b.blob(Vector3(-0.04, 0.004, -0.028), Vector3(0.01, 0.006, 0.01), 5, 3, ROCK, 0.1)
	b.pole(Vector3(0.012, 0, -0.04), Vector3(0.03, 0.036, -0.036), 0.003, WOOD_LIGHT)
	b.box(Vector3(0.03, 0.038, -0.036), Vector3(0.016, 0.006, 0.006), STONE_DARK)
	meshes["decor_quarry"] = b.commit()

	# sky pole: a gnomon in a ring of small stones, a gold sun disc on top
	b = _b(717)
	b.pole(Vector3.ZERO, Vector3(0, 0.1, 0), 0.0048, WOOD_LIGHT)
	for i in 8:
		var a := TAU * i / 8.0
		b.blob(Vector3(cos(a) * 0.06, 0.004, sin(a) * 0.06), Vector3(0.008, 0.007, 0.008), 5, 3, STONE, 0.1)
	b.box(Vector3(0, 0.106, 0), Vector3(0.022, 0.006, 0.022), Color("#c9973c"))
	meshes["decor_skypole"] = b.commit()

	# mudbricks drying in rows
	b = _b(718)
	for row in 3:
		for i in 4:
			var tone := 0.04 * ((row * 4 + i) % 3 - 1)
			b.box(Vector3(-0.03 + i * 0.02, 0.004 + (row % 2) * 0.0, -0.02 + row * 0.02), Vector3(0.017, 0.008, 0.009), MUD.lightened(tone))
	b.box(Vector3(0.0, 0.012, -0.012), Vector3(0.017, 0.008, 0.009), MUD.darkened(0.05))
	b.box(Vector3(0.02, 0.012, -0.012), Vector3(0.017, 0.008, 0.009), MUD.lightened(0.05))
	meshes["decor_bricks"] = b.commit()

	# baskets of nuts and seed
	b = _b(719)
	for bk in [[Vector3(-0.022, 0, 0), 0.0], [Vector3(0.016, 0, 0.012), 1.0], [Vector3(0.0, 0, -0.026), 2.0]]:
		var bp: Vector3 = bk[0]
		b.prism(bp, 0.012, 0.016, 0.018, 6, Color("#b08a4e"), Color("#7a5a2c"))
		b.blob(bp + Vector3(0, 0.019, 0), Vector3(0.012, 0.006, 0.012), 5, 3, Color("#7a5a30") if bk[1] != 1.0 else Color("#d6ae43"))
	meshes["decor_baskets"] = b.commit()

	# woodwork: a chopping block with an adze, planks and a stack of coppiced poles
	b = _b(720)
	b.prism(Vector3.ZERO, 0.02, 0.02, 0.02, 7, WOOD_LIGHT, WOOD_LIGHT.lightened(0.15))
	b.pole(Vector3(0.006, 0.02, 0.004), Vector3(0.03, 0.036, 0.012), 0.0024, WOOD)
	b.box(Vector3(0.032, 0.037, 0.012), Vector3(0.012, 0.005, 0.008), STONE_DARK)
	b.box(Vector3(-0.045, 0.004, 0.02), Vector3(0.05, 0.008, 0.012), WOOD_LIGHT.lightened(0.1))
	b.box(Vector3(-0.045, 0.012, 0.02), Vector3(0.05, 0.008, 0.012), WOOD_LIGHT)
	for i in 4:
		b.pole(Vector3(0.03 + i * 0.006, 0, -0.03), Vector3(0.032 + i * 0.006, 0.07, -0.034), 0.0028, WOOD if i % 2 == 0 else WOOD_LIGHT)
	meshes["decor_woodwork"] = b.commit()


# ------------------------------------------------------------------ megalith pieces (see monument_rig.gd)

func _monument() -> void:
	# a standing stone, foot at the origin, 0.17 tall: rough, tapering, with a band of red ochre
	var b := _b(730)
	b.prism(Vector3.ZERO, 0.034, 0.03, 0.11, 6, ROCK, ROCK_HI, 0.0, 0.1)
	b.prism(Vector3(0, 0.11, 0), 0.03, 0.019, 0.06, 6, ROCK_HI, ROCK_HI.lightened(0.08), 0.3, 0.1)
	b.prism(Vector3(0, 0.07, 0), 0.0345, 0.0345, 0.008, 6, Color("#a4452c"), Color("#a4452c"))
	meshes["monument_menhir"] = b.commit()
	# a lintel, centred, long axis along X
	b = _b(731)
	b.box(Vector3.ZERO, Vector3(0.26, 0.04, 0.06), ROCK_HI, ROCK_HI.lightened(0.1))
	b.box(Vector3(0, -0.012, 0), Vector3(0.262, 0.008, 0.062), Color("#a4452c"))
	meshes["monument_lintel"] = b.commit()
	# a drag sledge, centred, long axis along Z: two runners and cross boards
	b = _b(732)
	b.pole(Vector3(-0.03, 0.006, -0.1), Vector3(-0.03, 0.006, 0.09), 0.0045, WOOD)
	b.pole(Vector3(0.03, 0.006, -0.1), Vector3(0.03, 0.006, 0.09), 0.0045, WOOD)
	b.pole(Vector3(-0.03, 0.006, 0.09), Vector3(-0.03, 0.02, 0.112), 0.0045, WOOD)
	b.pole(Vector3(0.03, 0.006, 0.09), Vector3(0.03, 0.02, 0.112), 0.0045, WOOD)
	for z in [-0.08, -0.03, 0.02, 0.07]:
		b.box(Vector3(0, 0.014, z), Vector3(0.08, 0.008, 0.014), WOOD_LIGHT)
	meshes["monument_sledge"] = b.commit()
	# a roller log, axis along X
	b = _b(733)
	b.pole(Vector3(-0.045, 0.0, 0), Vector3(0.045, 0.0, 0), 0.0075, WOOD_LIGHT, 6)
	meshes["monument_roller"] = b.commit()
	# a drag rope, one unit long along +Z from the origin
	b = _b(734)
	b.box(Vector3(0, 0, 0.5), Vector3(0.004, 0.004, 1.0), Color("#b9a68a"))
	meshes["monument_rope"] = b.commit()


# ------------------------------------------------------------------ boats under fishers (named person_* so they glide with them)

## Hulls lie along +Z, origin at the waterline, about 0.2 long. Tiers: 1 raft, 2 bark canoe, 3 dugout, 4 outrigger.
func _boats() -> void:
	var cord := Color("#b9a68a")
	var b := _b(740)
	for i in 5:
		var x := -0.044 + i * 0.022
		b.pole(Vector3(x, 0.006, -0.095), Vector3(x, 0.006, 0.095), 0.0115, WOOD_LIGHT if i % 2 == 0 else WOOD, 6)
	for z in [-0.045, 0.045]:
		b.box(Vector3(0, 0.0165, z), Vector3(0.112, 0.004, 0.007), cord)
	meshes["person_boat_1"] = b.commit()

	b = _b(741)
	b.blob(Vector3(0, 0.008, 0), Vector3(0.027, 0.014, 0.105), 8, 3, Color("#b88a56"), 0.0, Color("#8a6238"))
	b.blob(Vector3(0, 0.014, 0), Vector3(0.017, 0.007, 0.09), 8, 3, Color("#4a3322"))
	b.prism(Vector3(0, 0.01, 0.1), 0.008, 0.0, 0.03, 4, Color("#b88a56"), Color("#b88a56"))
	b.prism(Vector3(0, 0.01, -0.1), 0.008, 0.0, 0.03, 4, Color("#b88a56"), Color("#b88a56"))
	meshes["person_boat_2"] = b.commit()

	b = _b(742)
	b.blob(Vector3(0, 0.01, 0), Vector3(0.034, 0.017, 0.125), 8, 3, WOOD_LIGHT, 0.0, WOOD)
	b.blob(Vector3(0, 0.017, 0), Vector3(0.022, 0.008, 0.105), 8, 3, Color("#3a2c1f"))
	for z in [-0.045, 0.0, 0.045]:
		b.box(Vector3(0, 0.02, z), Vector3(0.06, 0.004, 0.008), WOOD)
	meshes["person_boat_3"] = b.commit()

	b = _b(743)
	b.blob(Vector3(0, 0.009, 0), Vector3(0.026, 0.015, 0.13), 8, 3, WOOD_LIGHT, 0.0, WOOD)
	b.blob(Vector3(0, 0.015, 0), Vector3(0.016, 0.007, 0.11), 8, 3, Color("#3a2c1f"))
	b.pole(Vector3(0.066, 0.008, -0.085), Vector3(0.066, 0.008, 0.085), 0.0075, WOOD_LIGHT, 6)
	for z in [-0.045, 0.045]:
		b.pole(Vector3(0.012, 0.016, z), Vector3(0.066, 0.012, z), 0.0028, WOOD)
	meshes["person_boat_4"] = b.commit()
	# the same hulls for boats at rest (plain props, not figures)
	for tier in range(1, 5):
		meshes["boat_%d" % tier] = meshes["person_boat_%d" % tier]


# ------------------------------------------------------------------ the feast (see feast_rig.gd)

## One guest at a feast, local frame like the other people (+Y up, forward +Z). pose: "seated" cross-legged with a bowl, "cheer"
## standing with arms raised, "host" standing with arms flung wide and a cup.
func _feaster(seed: int, pose: String) -> ArrayMesh:
	var b := _b(seed)
	var legc := Color("#4a3a2a")
	var save := b.xf
	if pose == "seated":
		# knees up in front, the body sitting on the ground
		b.pole(Vector3(-0.009, 0.007, 0.0), Vector3(-0.014, 0.007, 0.034), 0.0065, legc)
		b.pole(Vector3(0.009, 0.007, 0.0), Vector3(0.014, 0.007, 0.034), 0.0065, legc)
		b.xf = b.xf * Transform3D(Basis(), Vector3(0, -0.02, 0))
	else:
		b.prism(Vector3(-0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, legc, legc)
		b.prism(Vector3(0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, legc, legc)
		b.xf = b.xf * Transform3D(Basis(), Vector3(0, 0.034, 0))
	b.tag = M7MeshBuilder.TAG_CLOTH
	b.prism(Vector3(0, 0, 0), 0.018, 0.015, 0.036, 7, Color("#8a6a48"), Color("#8a6a48"))
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.02, 0), 0.0215, 0.009, 0.022, 7, Color.WHITE, Color.WHITE)
	b.prism(Vector3(0, 0.002, 0), 0.0188, 0.0185, 0.007, 7, Color.WHITE, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	match pose:
		"seated":
			b.pole(Vector3(-0.02, 0.03, 0), Vector3(-0.012, 0.012, 0.026), 0.0045, SKIN)
			b.pole(Vector3(0.02, 0.03, 0), Vector3(0.012, 0.012, 0.026), 0.0045, SKIN)
			b.prism(Vector3(0, 0.004, 0.028), 0.011, 0.014, 0.011, 6, Color("#b5603c"), Color("#7a3a20"))
		"cheer":
			b.pole(Vector3(-0.02, 0.03, 0), Vector3(-0.032, 0.074, 0.004), 0.0045, SKIN)
			b.pole(Vector3(0.02, 0.03, 0), Vector3(0.032, 0.074, 0.004), 0.0045, SKIN)
		"host":
			b.pole(Vector3(-0.02, 0.032, 0), Vector3(-0.062, 0.05, 0.0), 0.0048, SKIN)
			b.pole(Vector3(0.02, 0.032, 0), Vector3(0.062, 0.05, 0.0), 0.0048, SKIN)
			b.prism(Vector3(0.062, 0.05, 0.0), 0.0075, 0.0095, 0.012, 6, Color("#c9973c"), Color("#c9973c"))
	b.blob(Vector3(0, 0.05, 0), Vector3(0.0125, 0.013, 0.0125), 7, 4, SKIN, 0.0)
	b.blob(Vector3(0, 0.055, -0.003), Vector3(0.0128, 0.009, 0.012), 7, 3, HAIR, 0.0)
	b.xf = save
	return b.commit()

func _feast() -> void:
	meshes["person_feast_seated"] = _feaster(750, "seated")
	meshes["person_feast_cheer"] = _feaster(751, "cheer")
	meshes["person_feast_host"] = _feaster(752, "host")
	# the long mat, laid out along X: bowls, jars, a roast on a spit, and a ring of stones for the fire at the -X end
	var b := _b(753)
	b.box(Vector3(0, 0.002, 0), Vector3(0.3, 0.004, 0.085), Color("#d7c29a"), Color("#e4d2ac"))
	b.box(Vector3(0, 0.0035, 0), Vector3(0.3, 0.001, 0.012), Color("#9c4a32"))
	for i in 6:
		var x := -0.09 + i * 0.036
		var z := 0.022 if i % 2 == 0 else -0.022
		b.prism(Vector3(x, 0.004, z), 0.0085, 0.011, 0.009, 6, Color("#b5603c") if i % 3 else Color("#c27a4a"), Color("#6e3a22"))
	for jar in [[0.02, 0.0], [0.065, 0.0], [-0.04, 0.0]]:
		b.blob(Vector3(jar[0], 0.012, jar[1]), Vector3(0.011, 0.013, 0.011), 7, 4, Color("#a4532f"), 0.0, Color("#7a3a20"))
	b.pole(Vector3(0.1, 0.014, -0.032), Vector3(0.1, 0.014, 0.032), 0.0016, WOOD)
	b.blob(Vector3(0.1, 0.018, 0.0), Vector3(0.012, 0.01, 0.026), 6, 3, Color("#8a4a2a"), 0.0, Color("#5a2f1a"))
	for i in 7:
		var a := TAU * i / 7.0
		b.blob(Vector3(-0.135 + cos(a) * 0.026, 0.006, sin(a) * 0.026), Vector3(0.008, 0.007, 0.008), 5, 3, STONE, 0.1)
	meshes["feast_mat"] = b.commit()
