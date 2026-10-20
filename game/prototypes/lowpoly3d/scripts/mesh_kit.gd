## Every prop, building, figure and animal, generated in code (no assets).
## Units: 1.0 = one tile column. An adult figure is ~0.1 tall, a hut ~0.15.
class_name MeshKit
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

func _init() -> void:
	_trees()
	_ground_cover()
	_mountains()
	_structures()
	_people()
	_animals()

func get_mesh(name: String) -> ArrayMesh:
	return meshes[name]

func _b(seed: int = 1) -> MeshBuilder:
	var b := MeshBuilder.new()
	b.seed = seed
	return b

# ------------------------------------------------------------------ vegetation

func _trees() -> void:
	for k in 3:
		var b := _b(11 + k * 7)
		b.prism(Vector3.ZERO, 0.016, 0.012, 0.07, 5, BARK, BARK)
		b.tag = MeshBuilder.TAG_EVERGREEN
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
		b.tag = MeshBuilder.TAG_FOLIAGE
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
		b.tag = MeshBuilder.TAG_FOLIAGE
		b.blob(top + Vector3(0.0, 0.05, 0.0), Vector3(0.13, 0.03, 0.105), 8, 3, ACACIA, 0.1, ACACIA.darkened(0.25))
		b.blob(top + Vector3(0.05, 0.065, 0.01), Vector3(0.07, 0.022, 0.06), 6, 3, ACACIA.lightened(0.06), 0.1, ACACIA.darkened(0.2))
		meshes["acacia%d" % k] = b.commit()
	for k in 2:
		var b := _b(71 + k * 9)
		b.prism(Vector3.ZERO, 0.02, 0.013, 0.17, 5, BARK.lightened(0.1), BARK)
		b.tag = MeshBuilder.TAG_EVERGREEN
		b.blob(Vector3(0, 0.2, 0), Vector3(0.1, 0.07, 0.1), 7, 4, JUNGLE, 0.18, JUNGLE.darkened(0.3))
		b.blob(Vector3(0.06, 0.16, 0.03), Vector3(0.07, 0.05, 0.07), 6, 3, JUNGLE.lightened(0.08), 0.18, JUNGLE.darkened(0.3))
		b.blob(Vector3(-0.05, 0.17, -0.04), Vector3(0.065, 0.05, 0.065), 6, 3, JUNGLE.lightened(0.04), 0.18, JUNGLE.darkened(0.3))
		meshes["jungle%d" % k] = b.commit()
	# bushes
	var bb := _b(91)
	bb.tag = MeshBuilder.TAG_FOLIAGE
	bb.blob(Vector3(0, 0.025, 0), Vector3(0.05, 0.035, 0.045), 6, 3, LEAF.darkened(0.1), 0.2, LEAF_DARK.darkened(0.1))
	bb.blob(Vector3(0.035, 0.018, 0.01), Vector3(0.03, 0.025, 0.03), 5, 3, LEAF, 0.2)
	meshes["bush"] = bb.commit()
	var bt := _b(93)
	bt.tag = MeshBuilder.TAG_FOLIAGE
	bt.blob(Vector3(0, 0.018, 0), Vector3(0.035, 0.022, 0.032), 6, 3, Color("#7d7a45"), 0.3, Color("#5b5a35"))
	meshes["shrub"] = bt.commit()
	# orchard tree: small round canopy with fruit
	var ob := _b(95)
	ob.prism(Vector3.ZERO, 0.009, 0.007, 0.05, 5, BARK, BARK)
	ob.tag = MeshBuilder.TAG_FOLIAGE
	ob.blob(Vector3(0, 0.075, 0), Vector3(0.045, 0.04, 0.045), 7, 4, LEAF.lightened(0.05), 0.08, LEAF_DARK)
	ob.tag = MeshBuilder.TAG_PLAIN
	for i in 5:
		var a := TAU * i / 5.0 + 0.3
		ob.blob(Vector3(cos(a) * 0.038, 0.07 + 0.015 * sin(a * 3.0), sin(a) * 0.038), Vector3(0.008, 0.008, 0.008), 4, 2, Color("#c0392b"))
	meshes["orchard_tree"] = ob.commit()

func _ground_cover() -> void:
	for k in 2:
		var b := _b(101 + k)
		b.tag = MeshBuilder.TAG_GRASS
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
		g.tag = MeshBuilder.TAG_GRASS
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
	rb.tag = MeshBuilder.TAG_GRASS
	for i in 9:
		var a := rb.rnd() * TAU
		var r := 0.03 * rb.rnd()
		var base := Vector3(cos(a) * r, 0, sin(a) * r)
		var h := 0.06 + 0.04 * rb.rnd()
		var side := Vector3(-sin(a), 0, cos(a)) * 0.004
		rb.tri(base - side, base + side, base + Vector3(cos(a) * 0.01, h, sin(a) * 0.01), REED)
		if i % 3 == 0:
			rb.tag = MeshBuilder.TAG_PLAIN
			rb.blob(base + Vector3(cos(a) * 0.009, h * 0.85, sin(a) * 0.009), Vector3(0.004, 0.012, 0.004), 4, 2, Color("#6b4a2e"))
			rb.tag = MeshBuilder.TAG_GRASS
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
func _peak(b: MeshBuilder, p: Vector3, r: float, h: float, sides: int, snow_frac: float) -> void:
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
	b.tag = MeshBuilder.TAG_EMISSIVE
	b.prism(Vector3(0, 0.0, 0), 0.024, 0.02, 0.006, 6, Color("#ff5a1a"), Color("#ff7a20"))
	meshes["hearth"] = b.commit()

	b = _b(303)
	b.tag = MeshBuilder.TAG_EMISSIVE
	b.prism(Vector3.ZERO, 0.02, 0.0, 0.075, 5, Color("#ff6a10"), Color("#ff6a10"), 0.0, 0.2)
	b.prism(Vector3(0.006, 0, 0.004), 0.012, 0.0, 0.05, 5, Color("#ffc040"), Color("#ffc040"), 0.5, 0.2)
	b.prism(Vector3(-0.008, 0, -0.003), 0.01, 0.0, 0.04, 4, Color("#ff9a20"), Color("#ff9a20"), 0.2, 0.2)
	meshes["flame"] = b.commit()

	# lean-to / brush shelter: a low dome of branches over a pole frame, open toward the hearth
	b = _b(305)
	b.blob(Vector3(0, 0.0, 0), Vector3(0.075, 0.07, 0.065), 9, 4, Color("#8c7445"), 0.12, Color("#6e5a36"))
	# loose branches laid over the dome
	for i in 9:
		var a := TAU * i / 9.0 + 0.2
		var p0 := Vector3(cos(a) * 0.072, 0.006, sin(a) * 0.062)
		var p1 := Vector3(cos(a + 0.5) * 0.02, 0.072, sin(a + 0.5) * 0.018)
		b.pole(p0, p1, 0.0035, Color("#5e4a2e") if i % 2 == 0 else Color("#7a6440"))
	b.box(Vector3(0, 0.022, 0.06), Vector3(0.032, 0.044, 0.012), Color("#2a2119"))
	for i in 3:
		var a := TAU * i / 3.0 + 0.9
		b.pole(Vector3(cos(a) * 0.05, 0.0, sin(a) * 0.05), Vector3(cos(a) * -0.005, 0.1, sin(a) * -0.005), 0.0035, WOOD)
	meshes["leanto"] = b.commit()

	# hide tent (tipi) with a civ band and protruding poles
	b = _b(307)
	b.prism(Vector3.ZERO, 0.062, 0.012, 0.13, 8, HIDE, HIDE)
	b.tag = MeshBuilder.TAG_CIV
	var band_y0 := 0.055
	var band_y1 := 0.075
	for i in 8:
		var a0 := TAU * i / 8.0
		var a1 := TAU * (i + 1) / 8.0
		var rr0 := 0.062 - (0.05) * band_y0 / 0.13 + 0.0015
		var rr1 := 0.062 - (0.05) * band_y1 / 0.13 + 0.0015
		b.quad(Vector3(cos(a0) * rr0, band_y0, sin(a0) * rr0), Vector3(cos(a1) * rr0, band_y0, sin(a1) * rr0),
			Vector3(cos(a1) * rr1, band_y1, sin(a1) * rr1), Vector3(cos(a0) * rr1, band_y1, sin(a0) * rr1), Color.WHITE, Vector3(0, 0.065, 0))
	b.tag = MeshBuilder.TAG_PLAIN
	for i in 4:
		var a := TAU * i / 4.0 + 0.4
		b.pole(Vector3(cos(a) * 0.008, 0.12, sin(a) * 0.008), Vector3(cos(a) * -0.012, 0.17, sin(a) * -0.012), 0.0035, WOOD)
	# door flap (dark triangle facing +Z)
	b.tri(Vector3(-0.022, 0.001, 0.058), Vector3(0.022, 0.001, 0.058), Vector3(0.0, 0.055, 0.044), Color("#3a2c1f"), Vector3(0, 0.03, 0))
	meshes["tent"] = b.commit()

	# pit house: sunken, turf-covered dome with an entrance and smoke hole
	b = _b(309)
	b.prism(Vector3.ZERO, 0.095, 0.09, 0.012, 9, EARTH, EARTH)
	b.tag = MeshBuilder.TAG_GRASS
	b.blob(Vector3(0, 0.012, 0), Vector3(0.088, 0.06, 0.088), 9, 4, Color("#7f8c48"), 0.06, EARTH)
	b.tag = MeshBuilder.TAG_PLAIN
	b.prism(Vector3(0, 0.064, 0), 0.016, 0.014, 0.012, 6, WOOD, Color("#2a2119"))
	b.box(Vector3(0, 0.022, 0.085), Vector3(0.04, 0.04, 0.035), WOOD_LIGHT, THATCH_DARK)
	b.box(Vector3(0, 0.017, 0.103), Vector3(0.022, 0.03, 0.002), Color("#2a2119"))
	meshes["pithouse"] = b.commit()

	# wattle-and-daub roundhouse with conical thatch
	b = _b(311)
	b.prism(Vector3.ZERO, 0.07, 0.07, 0.055, 10, DAUB, DAUB)
	b.prism(Vector3(0, 0.05, 0), 0.088, 0.0, 0.1, 10, THATCH, THATCH, 0.0, 0.03)
	b.box(Vector3(0, 0.022, 0.069), Vector3(0.026, 0.042, 0.004), Color("#3a2c1f"))
	meshes["wattle"] = b.commit()

	# mudbrick house: box with a flat roof and parapet
	b = _b(313)
	b.box(Vector3(0, 0.035, 0), Vector3(0.13, 0.07, 0.1), MUD)
	b.box(Vector3(0, 0.073, 0), Vector3(0.14, 0.008, 0.11), MUD.darkened(0.15), MUD.lightened(0.05))
	b.box(Vector3(0.02, 0.025, 0.051), Vector3(0.026, 0.05, 0.003), Color("#3a2c1f"))
	b.box(Vector3(-0.035, 0.05, 0.051), Vector3(0.018, 0.014, 0.003), Color("#3a2c1f"))
	meshes["mudbrick"] = b.commit()

	# longhouse: long low walls and a steep gabled thatch roof
	b = _b(315)
	b.box(Vector3(0, 0.025, 0), Vector3(0.26, 0.05, 0.1), WOOD_LIGHT)
	var L := 0.145
	var W := 0.072
	var ry := 0.13
	var e0 := 0.045
	b.quad(Vector3(-L, e0, W), Vector3(L, e0, W), Vector3(L, ry, 0), Vector3(-L, ry, 0), THATCH, Vector3(0, 0.0, -0.1))
	b.quad(Vector3(-L, e0, -W), Vector3(L, e0, -W), Vector3(L, ry, 0), Vector3(-L, ry, 0), THATCH.darkened(0.03), Vector3(0, 0.0, 0.1))
	b.tri(Vector3(-0.13, 0.05, 0.05), Vector3(-0.13, 0.05, -0.05), Vector3(-0.13, ry - 0.01, 0), WOOD, Vector3(0, 0.07, 0))
	b.tri(Vector3(0.13, 0.05, 0.05), Vector3(0.13, 0.05, -0.05), Vector3(0.13, ry - 0.01, 0), WOOD, Vector3(0, 0.07, 0))
	b.box(Vector3(0.0, 0.02, 0.051), Vector3(0.024, 0.04, 0.003), Color("#3a2c1f"))
	meshes["longhouse"] = b.commit()

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
	b.tag = MeshBuilder.TAG_EMISSIVE
	b.box(Vector3(0, 0.012, 0.047), Vector3(0.02, 0.018, 0.004), Color("#ff6a20"))
	meshes["kiln"] = b.commit()

	# quern: slab and a round upper stone
	b = _b(325)
	b.prism(Vector3.ZERO, 0.03, 0.028, 0.01, 6, STONE_DARK, STONE)
	b.prism(Vector3(0, 0.01, 0), 0.018, 0.017, 0.01, 8, STONE, STONE.lightened(0.15))
	meshes["quern"] = b.commit()

	# thorn hedge segment (along local X, ~0.12 long)
	b = _b(327)
	b.tag = MeshBuilder.TAG_FOLIAGE
	for i in 4:
		var x := -0.05 + i * 0.033 + 0.006 * b.rnds()
		b.blob(Vector3(x, 0.018, 0.004 * b.rnds()), Vector3(0.022, 0.022, 0.018), 5, 3, Color("#5d6234"), 0.5, Color("#4a4a2a"))
	b.tag = MeshBuilder.TAG_PLAIN
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
	var fr := WorldData.R * 0.84
	var hexp := []
	for i in 6:
		hexp.append(WorldData.corner(i) * 0.84)
	for i in 6:
		var p0: Vector2 = hexp[i]
		var p1: Vector2 = hexp[(i + 1) % 6]
		b.tri(Vector3(0, 0.004, 0), Vector3(p0.x, 0.004, p0.y), Vector3(p1.x, 0.004, p1.y), SOIL, Vector3(0, -1, 0))
	b.tag = MeshBuilder.TAG_CROP
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
	b.tag = MeshBuilder.TAG_CIV
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
	b.tag = MeshBuilder.TAG_CIV
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
	b.tag = MeshBuilder.TAG_PLAIN
	var cx := fw * 0.5
	var cy := (y0 + y1) * 0.5
	var s := fh * 0.42
	for poly in Emblems.polygons(civ):
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
func _person(seed: int, tool: String, stoop: float = 0.0, elder: bool = false) -> ArrayMesh:
	var b := _b(seed)
	var legc := Color("#4a3a2a")
	# legs
	b.prism(Vector3(-0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, legc, legc)
	b.prism(Vector3(0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, legc, legc)
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis(Vector3.RIGHT, stoop), Vector3(0, 0.034, 0))
	# torso (hide tunic, toned per instance)
	b.tag = MeshBuilder.TAG_CLOTH
	b.prism(Vector3(0, 0, 0), 0.018, 0.015, 0.036, 7, Color("#8a6a48"), Color("#8a6a48"))
	# civ mantle over the shoulders: the main identity read from above
	b.tag = MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.02, 0), 0.0215, 0.009, 0.022, 7, Color.WHITE, Color.WHITE)
	# sash diagonal band
	b.prism(Vector3(0, 0.002, 0), 0.0188, 0.0185, 0.007, 7, Color.WHITE, Color.WHITE)
	# arms
	b.tag = MeshBuilder.TAG_PLAIN
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
			b.prism(hand + Vector3(0.004, 0.07, 0.012), 0.005, 0.0, 0.016, 4, STONE_DARK, STONE_DARK)
		"basket":
			b.prism(Vector3(-0.026, -0.012, 0.004), 0.011, 0.014, 0.016, 6, Color("#b08a4e"), Color("#5a7a2c"))
		"axe":
			b.pole(hand, hand + Vector3(0.006, 0.045, 0.014), 0.0026, WOOD_LIGHT)
			b.box(hand + Vector3(0.012, 0.043, 0.014), Vector3(0.014, 0.01, 0.005), STONE_DARK)
		"hoe":
			b.pole(hand + Vector3(0, -0.03, -0.006), hand + Vector3(0.002, 0.05, 0.01), 0.0026, WOOD_LIGHT)
			b.box(hand + Vector3(0.002, -0.032, 0.0), Vector3(0.006, 0.006, 0.016), STONE_DARK)
		"pole":
			b.pole(hand, hand + Vector3(0.01, 0.06, 0.07), 0.0022, WOOD_LIGHT)
		"hammer":
			b.pole(hand, hand + Vector3(0.0, 0.03, 0.012), 0.0026, WOOD_LIGHT)
			b.box(hand + Vector3(0.0, 0.032, 0.012), Vector3(0.014, 0.01, 0.01), STONE)
		"bundle":
			b.box(Vector3(0, 0.02, -0.022), Vector3(0.026, 0.026, 0.016), Color("#a5814f"))
	b.xf = save
	if tool == "stick":
		b.pole(Vector3(0.03, 0, 0.02), Vector3(0.026, 0.06, 0.016), 0.0026, WOOD_LIGHT)
	return b.commit()

func _people() -> void:
	for t in ["", "spear", "basket", "axe", "hoe", "pole", "hammer", "bundle"]:
		meshes["person_" + (t if t != "" else "plain")] = _person(500 + t.length(), t)
	meshes["person_elder"] = _person(551, "stick", 0.3, true)
	# child: scaled-down proportions with a larger head
	var b := _b(561)
	var legc := Color("#4a3a2a")
	b.prism(Vector3(-0.005, 0, 0), 0.0045, 0.005, 0.02, 5, legc, legc)
	b.prism(Vector3(0.005, 0, 0), 0.0045, 0.005, 0.02, 5, legc, legc)
	b.tag = MeshBuilder.TAG_CLOTH
	b.prism(Vector3(0, 0.019, 0), 0.012, 0.0105, 0.024, 7, Color("#9a7a55"), Color("#9a7a55"))
	b.tag = MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.032, 0), 0.0135, 0.007, 0.012, 7, Color.WHITE, Color.WHITE)
	b.tag = MeshBuilder.TAG_PLAIN
	b.blob(Vector3(0, 0.05, 0), Vector3(0.011, 0.0115, 0.011), 7, 4, SKIN, 0.0)
	b.blob(Vector3(0, 0.054, -0.003), Vector3(0.0112, 0.008, 0.0105), 7, 3, HAIR, 0.0)
	meshes["person_child"] = b.commit()
	# lying figure on a hide mat (convalescing)
	b = _b(571)
	b.box(Vector3(0, 0.003, 0), Vector3(0.05, 0.004, 0.1), HIDE_DARK)
	b.tag = MeshBuilder.TAG_CLOTH
	b.blob(Vector3(0, 0.012, 0.0), Vector3(0.016, 0.01, 0.04), 6, 3, Color("#8a6a48"))
	b.tag = MeshBuilder.TAG_CIV
	b.blob(Vector3(0, 0.016, -0.012), Vector3(0.018, 0.009, 0.022), 6, 3, Color.WHITE)
	b.tag = MeshBuilder.TAG_PLAIN
	b.blob(Vector3(0, 0.013, -0.045), Vector3(0.012, 0.011, 0.012), 6, 3, SKIN)
	meshes["person_lying"] = b.commit()
	# soft contact shadow disc under figures (dark, flat)
	b = _b(573)
	for i in 8:
		var a0 := TAU * i / 8.0
		var a1 := TAU * (i + 1) / 8.0
		b.tri(Vector3(0, 0.002, 0), Vector3(cos(a0) * 0.022, 0.002, sin(a0) * 0.022), Vector3(cos(a1) * 0.022, 0.002, sin(a1) * 0.022), Color("#20180f"), Vector3(0, -1, 0))
	meshes["shadow_disc"] = b.commit()

# ------------------------------------------------------------------ animals (facing +X, body in civ/species colour)

func _animals() -> void:
	# heavy: aurochs, bison, boar, horse
	var b := _b(601)
	var legc := Color("#3a2e24")
	for p in [Vector3(-0.03, 0, -0.012), Vector3(-0.03, 0, 0.012), Vector3(0.028, 0, -0.012), Vector3(0.028, 0, 0.012)]:
		b.prism(p, 0.006, 0.007, 0.04, 4, legc, legc)
	b.tag = MeshBuilder.TAG_CIV
	b.blob(Vector3(0, 0.052, 0), Vector3(0.05, 0.024, 0.022), 7, 4, Color.WHITE, 0.08)
	b.blob(Vector3(0.022, 0.066, 0), Vector3(0.025, 0.018, 0.019), 6, 3, Color.WHITE, 0.08)  # shoulder hump
	b.blob(Vector3(0.058, 0.05, 0), Vector3(0.016, 0.013, 0.012), 6, 3, Color.WHITE, 0.05)    # head
	b.tag = MeshBuilder.TAG_PLAIN
	b.pole(Vector3(0.06, 0.06, -0.008), Vector3(0.066, 0.072, -0.02), 0.0025, CREAM)
	b.pole(Vector3(0.06, 0.06, 0.008), Vector3(0.066, 0.072, 0.02), 0.0025, CREAM)
	meshes["animal_heavy"] = b.commit()
	# slender: deer, reindeer, gazelle, goat/sheep, camel/llama
	b = _b(611)
	for p in [Vector3(-0.024, 0, -0.008), Vector3(-0.024, 0, 0.008), Vector3(0.022, 0, -0.008), Vector3(0.022, 0, 0.008)]:
		b.prism(p, 0.0038, 0.0045, 0.048, 4, legc, legc)
	b.tag = MeshBuilder.TAG_CIV
	b.blob(Vector3(0, 0.056, 0), Vector3(0.036, 0.016, 0.014), 7, 4, Color.WHITE, 0.05)
	b.pole(Vector3(0.026, 0.06, 0), Vector3(0.04, 0.085, 0), 0.006, Color.WHITE)
	b.blob(Vector3(0.046, 0.088, 0), Vector3(0.013, 0.008, 0.008), 6, 3, Color.WHITE, 0.0)
	b.tag = MeshBuilder.TAG_PLAIN
	b.pole(Vector3(0.042, 0.094, -0.004), Vector3(0.036, 0.115, -0.012), 0.0018, Color("#d9cdb3"))
	b.pole(Vector3(0.042, 0.094, 0.004), Vector3(0.036, 0.115, 0.012), 0.0018, Color("#d9cdb3"))
	b.blob(Vector3(-0.036, 0.058, 0), Vector3(0.004, 0.004, 0.004), 4, 2, CREAM)
	meshes["animal_slender"] = b.commit()
	# woolly (sheep/goat)
	b = _b(621)
	for p in [Vector3(-0.016, 0, -0.008), Vector3(-0.016, 0, 0.008), Vector3(0.016, 0, -0.008), Vector3(0.016, 0, 0.008)]:
		b.prism(p, 0.0035, 0.004, 0.026, 4, legc, legc)
	b.tag = MeshBuilder.TAG_CIV
	b.blob(Vector3(0, 0.036, 0), Vector3(0.03, 0.019, 0.019), 7, 4, Color.WHITE, 0.12)
	b.tag = MeshBuilder.TAG_PLAIN
	b.blob(Vector3(0.032, 0.042, 0), Vector3(0.011, 0.009, 0.008), 6, 3, Color("#3a3029"), 0.0)
	meshes["animal_woolly"] = b.commit()
	# small game
	b = _b(631)
	b.tag = MeshBuilder.TAG_CIV
	b.blob(Vector3(0, 0.012, 0), Vector3(0.016, 0.011, 0.01), 6, 3, Color.WHITE, 0.05)
	b.blob(Vector3(0.015, 0.02, 0), Vector3(0.007, 0.007, 0.006), 5, 3, Color.WHITE, 0.0)
	b.tag = MeshBuilder.TAG_PLAIN
	b.pole(Vector3(0.016, 0.025, -0.003), Vector3(0.012, 0.04, -0.004), 0.002, Color("#b9a68a"))
	b.pole(Vector3(0.016, 0.025, 0.003), Vector3(0.012, 0.04, 0.004), 0.002, Color("#b9a68a"))
	meshes["animal_small"] = b.commit()
