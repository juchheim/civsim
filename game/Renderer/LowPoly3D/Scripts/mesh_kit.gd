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
	_ziggurats()
	_boats()
	M7LooksKit.build(self)
	_feast()
	_dogs()
	_people()
	_animals()
	for art in ART_PASSES:
		art.build(self)

## Art passes run last and replace meshes by name (see the header of each file).
const ART_PASSES := [
	preload("res://Renderer/LowPoly3D/Scripts/art_dwellings.gd"),
	preload("res://Renderer/LowPoly3D/Scripts/art_yard.gd"),
	preload("res://Renderer/LowPoly3D/Scripts/art_decor.gd"),
	preload("res://Renderer/LowPoly3D/Scripts/art_landmarks.gd"),
	preload("res://Renderer/LowPoly3D/Scripts/art_deposits.gd"),
]

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
	# flame: the shared fire (hearths, signal posts, ziggurat); every other building is built in the art_*.gd passes
	var b := _b(303)
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	b.prism(Vector3.ZERO, 0.02, 0.0, 0.075, 5, Color("#ff6a10"), Color("#ff6a10"), 0.0, 0.2)
	b.prism(Vector3(0.006, 0, 0.004), 0.012, 0.0, 0.05, 5, Color("#ffc040"), Color("#ffc040"), 0.5, 0.2)
	b.prism(Vector3(-0.008, 0, -0.003), 0.01, 0.0, 0.04, 4, Color("#ff9a20"), Color("#ff9a20"), 0.2, 0.2)
	meshes["flame"] = b.commit()


	# palisade segment (pointed stakes)
	b = _b(329)
	for i in 7:
		var x := -0.054 + i * 0.018
		var h := 0.075 + 0.012 * b.rnd()
		b.prism(Vector3(x, 0, 0), 0.0075, 0.0075, h, 5, WOOD_LIGHT, WOOD_LIGHT)
		b.prism(Vector3(x, h, 0), 0.0075, 0.0, 0.016, 5, WOOD_LIGHT.lightened(0.1), WOOD)
	b.pole(Vector3(-0.06, 0.05, 0.008), Vector3(0.06, 0.05, 0.008), 0.004, WOOD)
	meshes["palisade"] = b.commit()


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

## `garment` (FIB-04 Tailored Clothing): a fitted parka with a fur hem and hood, sleeves and mittens, leggings and boots, instead
## of the short tunic and bare legs and arms.
func _person(seed: int, tool: String, stoop: float = 0.0, elder: bool = false, tier: int = 0, garment: bool = false) -> ArrayMesh:
	var b := _b(seed)
	# heads grow a little with each tier (a longer blade, a heavier head) so the step reads at a distance
	var grow := 1.0 + 0.18 * tier
	var head_color: Color = TOOL_TIER_COLORS[tier]
	var legc := M7LooksKit.LEGGING if garment else Color("#4a3a2a")
	# legs
	b.prism(Vector3(-0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, legc, legc)
	b.prism(Vector3(0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, legc, legc)
	if garment:
		for sx in [-0.0075, 0.0075]:
			b.prism(Vector3(sx, 0, 0), 0.0075, 0.0072, 0.011, 5, M7LooksKit.BOOT, M7LooksKit.BOOT)
			b.prism(Vector3(sx, 0.0105, 0), 0.0079, 0.0079, 0.003, 5, M7LooksKit.FUR, M7LooksKit.FUR)
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis(Vector3.RIGHT, stoop), Vector3(0, 0.034, 0))
	# torso (hide tunic, toned per instance)
	b.tag = M7MeshBuilder.TAG_CLOTH
	if garment:
		b.prism(Vector3(0, -0.013, 0), 0.0235, 0.0155, 0.049, 7, M7LooksKit.PARKA, M7LooksKit.PARKA)
		b.tag = M7MeshBuilder.TAG_PLAIN
		b.prism(Vector3(0, -0.013, 0), 0.0243, 0.0237, 0.005, 7, M7LooksKit.FUR, M7LooksKit.FUR)
		b.prism(Vector3(0, 0.009, 0), 0.0188, 0.0183, 0.004, 7, M7LooksKit.BOOT, M7LooksKit.BOOT)
	else:
		b.prism(Vector3(0, 0, 0), 0.018, 0.015, 0.036, 7, Color("#8a6a48"), Color("#8a6a48"))
	# civ mantle over the shoulders: the main identity read from above
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.02, 0), 0.0215, 0.009, 0.022, 7, Color.WHITE, Color.WHITE)
	# sash diagonal band
	if not garment:
		b.prism(Vector3(0, 0.002, 0), 0.0188, 0.0185, 0.007, 7, Color.WHITE, Color.WHITE)
	# arms
	if garment:
		b.tag = M7MeshBuilder.TAG_CLOTH
		b.pole(Vector3(-0.02, 0.03, 0), Vector3(-0.024, 0.006, 0.006), 0.0058, M7LooksKit.PARKA)
		b.pole(Vector3(0.02, 0.03, 0), Vector3(0.026, 0.008, 0.01), 0.0058, M7LooksKit.PARKA)
		b.tag = M7MeshBuilder.TAG_PLAIN
		b.blob(Vector3(-0.0245, 0.004, 0.0065), Vector3(0.0058, 0.0058, 0.0058), 5, 3, M7LooksKit.FUR)
		b.blob(Vector3(0.0265, 0.006, 0.0105), Vector3(0.0058, 0.0058, 0.0058), 5, 3, M7LooksKit.FUR)
	else:
		b.tag = M7MeshBuilder.TAG_PLAIN
		b.pole(Vector3(-0.02, 0.03, 0), Vector3(-0.024, 0.006, 0.006), 0.0045, SKIN)
		b.pole(Vector3(0.02, 0.03, 0), Vector3(0.026, 0.008, 0.01), 0.0045, SKIN)
	# head and hair
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.blob(Vector3(0, 0.05, 0), Vector3(0.0125, 0.013, 0.0125), 7, 4, SKIN, 0.0)
	var hc := Color("#cfcac0") if elder else HAIR
	b.blob(Vector3(0, 0.055, -0.003), Vector3(0.0128, 0.009, 0.012), 7, 3, hc, 0.0)
	if garment:
		# fur-trimmed hood around the back and sides of the head
		b.blob(Vector3(0, 0.048, -0.004), Vector3(0.0158, 0.0152, 0.0125), 7, 3, M7LooksKit.FUR, 0.0)
		b.tag = M7MeshBuilder.TAG_CLOTH
		b.blob(Vector3(0, 0.052, -0.009), Vector3(0.0152, 0.0145, 0.0112), 7, 3, M7LooksKit.PARKA, 0.0)
		b.tag = M7MeshBuilder.TAG_PLAIN
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
		"crook":
			# a herder's staff, crooked at the top
			b.pole(hand + Vector3(0, -0.03, -0.006), hand + Vector3(0.002, 0.066, 0.01), 0.0026, WOOD_LIGHT)
			b.pole(hand + Vector3(0.002, 0.066, 0.01), hand + Vector3(-0.012, 0.074, 0.012), 0.0024, WOOD_LIGHT)
			b.pole(hand + Vector3(-0.012, 0.074, 0.012), hand + Vector3(-0.016, 0.062, 0.012), 0.0022, WOOD_LIGHT)
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

## A child: scaled-down proportions with a larger head.
func _child(garment: bool) -> ArrayMesh:
	var b := _b(561)
	var legc := M7LooksKit.LEGGING if garment else Color("#4a3a2a")
	b.prism(Vector3(-0.005, 0, 0), 0.0045, 0.005, 0.02, 5, legc, legc)
	b.prism(Vector3(0.005, 0, 0), 0.0045, 0.005, 0.02, 5, legc, legc)
	b.tag = M7MeshBuilder.TAG_CLOTH
	if garment:
		b.prism(Vector3(0, 0.008, 0), 0.0155, 0.0105, 0.035, 7, M7LooksKit.PARKA, M7LooksKit.PARKA)
		b.tag = M7MeshBuilder.TAG_PLAIN
		b.prism(Vector3(0, 0.008, 0), 0.0162, 0.0158, 0.004, 7, M7LooksKit.FUR, M7LooksKit.FUR)
	else:
		b.prism(Vector3(0, 0.019, 0), 0.012, 0.0105, 0.024, 7, Color("#9a7a55"), Color("#9a7a55"))
	b.tag = M7MeshBuilder.TAG_CIV
	b.prism(Vector3(0, 0.032, 0), 0.0135, 0.007, 0.012, 7, Color.WHITE, Color.WHITE)
	b.tag = M7MeshBuilder.TAG_PLAIN
	b.blob(Vector3(0, 0.05, 0), Vector3(0.011, 0.0115, 0.011), 7, 4, SKIN, 0.0)
	b.blob(Vector3(0, 0.054, -0.003), Vector3(0.0112, 0.008, 0.0105), 7, 3, HAIR, 0.0)
	if garment:
		b.blob(Vector3(0, 0.048, -0.004), Vector3(0.0138, 0.0132, 0.0112), 7, 3, M7LooksKit.FUR, 0.0)
	return b.commit()

func _people() -> void:
	_carry_meshes()
	for garment in [false, true]:
		var sfx := "_g" if garment else ""
		for t in ["", "spear", "basket", "axe", "hoe", "pole", "crook", "hammer", "bundle"]:
			meshes["person_" + (t if t != "" else "plain") + sfx] = _person(500 + t.length(), t, 0.0, false, 0, garment)
			if t in ["spear", "axe", "hoe", "hammer"]:
				for tier in range(1, TOOL_TIERS):
					meshes["person_%s_t%d%s" % [t, tier, sfx]] = _person(500 + t.length(), t, 0.0, false, tier, garment)
		meshes["person_elder" + sfx] = _person(551, "stick", 0.3, true, 0, garment)
		meshes["person_child" + sfx] = _child(garment)
	# lying figure on a hide mat (convalescing)
	var b := _b(571)
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
	"sledge", "palisade", "spearrack", "grave", "council", "quarry", "skypole", "bricks", "baskets", "woodwork",
	"temple", "chiefhall", "woolbales", "chariot"]

# ------------------------------------------------------------------ megalith pieces (see monument_rig.gd)

# ------------------------------------------------------------------ ziggurat (BLD-08)

# ziggurat palette (sRGB)
const ZIG_BRICK := Color("#a3704a")      # baked-brick casing, first terrace
const ZIG_BRICK_MID := Color("#b98a5c")  # second terrace, stair walls
const ZIG_BRICK_HI := Color("#cb9f6c")   # third terrace, gate pylons
const ZIG_WALK := Color("#dcbb8a")       # terrace walkways: lighter than every wall
const ZIG_STAIR := Color("#e6cc9e")      # stair treads: the lightest masonry
const ZIG_RECESS := Color("#6f4a30")     # buttress recesses
const ZIG_BITUMEN := Color("#3b2b20")    # reed-and-bitumen course
const ZIG_LIME := Color("#f1e6cb")       # whitewashed summit shrine
const ZIG_MESHES := ["ziggurat_s1", "ziggurat_s2", "ziggurat_s3", "ziggurat"]
# apron = STONE, raw tops = MUD, ramp = EARTH, poles = WOOD/WOOD_LIGHT, crest = M7LooksKit.GOLD, DARK = #2a2119

## BLD-08 Stepped Pyramid: the ziggurat a civilization raises at its city, in three half-built stages and a finished one.
func _ziggurats() -> void:
	for stage in range(1, 5):
		meshes[ZIG_MESHES[stage - 1]] = _ziggurat(stage)

## Battered rectangular terrace on (0, y, cz): bottom half-extents (ax, az), top (bx, bz); top == null leaves it open.
func _zig_terrace(b: M7MeshBuilder, cz: float, y: float, h: float, ax: float, az: float, bx: float, bz: float,
		side: Color, top = null) -> void:
	var lo := [Vector3(-ax, y, cz - az), Vector3(ax, y, cz - az), Vector3(ax, y, cz + az), Vector3(-ax, y, cz + az)]
	var hi := [Vector3(-bx, y + h, cz - bz), Vector3(bx, y + h, cz - bz), Vector3(bx, y + h, cz + bz), Vector3(-bx, y + h, cz + bz)]
	var core := Vector3(0, y + h * 0.5, cz)
	for i in 4:                                   # 0 back, 1 +X, 2 front, 3 -X
		b.quad(lo[i], lo[(i + 1) % 4], hi[(i + 1) % 4], hi[i], side.darkened(0.05) if i % 2 == 1 else side, core)
	if top != null:
		b.quad(hi[0], hi[1], hi[2], hi[3], top, Vector3(0, y, cz))

## Dark recess stripes 1.5 mm proud of a battered wall: at absolute x on the back face, at cz + z on the side faces.
func _zig_stripes(b: M7MeshBuilder, cz: float, y: float, h: float, ax: float, az: float, bx: float, bz: float,
		back_xs: Array, side_zs: Array) -> void:
	var w := 0.008
	var e := 0.0015
	var y0 := y + 0.004
	var y1 := y + h - 0.004
	var core := Vector3(0, y + h * 0.5, cz)
	for x in back_xs:
		b.quad(Vector3(x - w, y0, cz - az - e), Vector3(x + w, y0, cz - az - e),
			Vector3(x + w, y1, cz - bz - e), Vector3(x - w, y1, cz - bz - e), ZIG_RECESS, core)
	for sx in [-1.0, 1.0]:
		for z in side_zs:
			b.quad(Vector3(sx * (ax + e), y0, cz + z - w), Vector3(sx * (ax + e), y0, cz + z + w),
				Vector3(sx * (bx + e), y1, cz + z + w), Vector3(sx * (bx + e), y1, cz + z - w), ZIG_RECESS, core)

## Axial flight (centred on x = 0) from foot (zf, yf) to head (zh, yh) against a terrace face whose foot is at zb:
## a tread slope, parapet walls each side (outer face, cap, inner face), `steps` dark tread lines.
func _zig_stair(b: M7MeshBuilder, zf: float, yf: float, zh: float, yh: float, zb: float, hw: float, steps: int) -> void:
	var p := 0.008
	var ww := hw + 0.006
	var zm := (zf + zh) * 0.5
	b.quad(Vector3(-hw, yf, zf), Vector3(hw, yf, zf), Vector3(hw, yh, zh), Vector3(-hw, yh, zh), ZIG_STAIR, Vector3(0, yf - 0.05, zm))
	for sx in [-1.0, 1.0]:
		b.quad(Vector3(sx * ww, yf, zf), Vector3(sx * ww, yf, zb), Vector3(sx * ww, yh + p, zh), Vector3(sx * ww, yf + p, zf),
			ZIG_BRICK_MID, Vector3(0, yf, zm))
		b.quad(Vector3(sx * hw, yf + p, zf), Vector3(sx * ww, yf + p, zf), Vector3(sx * ww, yh + p, zh), Vector3(sx * hw, yh + p, zh),
			ZIG_WALK, Vector3(sx * hw, yf - 0.05, zm))
		b.quad(Vector3(sx * hw, yf, zf), Vector3(sx * hw, yh, zh), Vector3(sx * hw, yh + p, zh), Vector3(sx * hw, yf + p, zf),
			ZIG_BRICK_MID.darkened(0.1), Vector3(sx * ww * 3.0, (yf + yh) * 0.5, zm))
	var d := 0.0035 / Vector2(zh - zf, yh - yf).length()
	for i in steps:
		var t := (i + 0.5) / steps
		var a := Vector3(0, lerpf(yf, yh, t - d) + 0.0012, lerpf(zf, zh, t - d))
		var c := Vector3(0, lerpf(yf, yh, t + d) + 0.0012, lerpf(zf, zh, t + d))
		b.quad(a + Vector3(-hw, 0, 0), a + Vector3(hw, 0, 0), c + Vector3(hw, 0, 0), c + Vector3(-hw, 0, 0),
			ZIG_STAIR.darkened(0.32), a + Vector3(0, -0.05, 0))

## The two side flights hugging the first terrace's front face, rising toward the central stair.
func _zig_side_flights(b: M7MeshBuilder) -> void:
	for sx in [-1.0, 1.0]:
		var f_in := Vector3(sx * 0.205, 0.012, 0.19)
		var f_out := Vector3(sx * 0.205, 0.012, 0.226)
		var h_in := Vector3(sx * 0.064, 0.122, 0.173)
		var h_out := Vector3(sx * 0.064, 0.122, 0.209)
		var under := Vector3(sx * 0.064, 0.012, 0.226)
		var core := Vector3(sx * 0.13, 0.03, 0.2)
		b.quad(f_in, f_out, h_out, h_in, ZIG_STAIR, core)
		b.tri(f_out, h_out, under, ZIG_BRICK_MID, core)
		b.quad(under, h_out, h_in, Vector3(sx * 0.064, 0.012, 0.19), ZIG_BRICK_MID.darkened(0.08), core)
		for i in 5:
			var t := (i + 0.5) / 5.0
			var a0 := f_in.lerp(h_in, t - 0.02) + Vector3(0, 0.0012, 0)
			var a1 := f_out.lerp(h_out, t - 0.02) + Vector3(0, 0.0012, 0)
			var c1 := f_out.lerp(h_out, t + 0.02) + Vector3(0, 0.0012, 0)
			var c0 := f_in.lerp(h_in, t + 0.02) + Vector3(0, 0.0012, 0)
			b.quad(a0, a1, c1, c0, ZIG_STAIR.darkened(0.32), a0 + Vector3(0, -0.05, 0))

func _zig_bricks(b: M7MeshBuilder, p: Vector3) -> void:
	b.box(p + Vector3(0, 0.006, 0), Vector3(0.032, 0.012, 0.022), MUD, MUD.lightened(0.08))
	b.box(p + Vector3(0.003, 0.018, -0.002), Vector3(0.024, 0.012, 0.018), MUD.darkened(0.06), MUD.lightened(0.04))

## Stage 1..3 = under construction, 4 = finished. Origin at ground centre, stair faces +Z (toward the hearth).
func _ziggurat(stage: int) -> ArrayMesh:
	var b := _b(790 + stage)
	var DARK := Color("#2a2119")
	# cut-stone apron, sunk to -0.038 so a slope never shows under it
	b.box(Vector3(0, -0.013, 0.0425), Vector3(0.5, 0.05, 0.515), STONE, STONE.lightened(0.08))
	# --- terrace 1: y 0.012 .. 0.122
	if stage == 1:
		_zig_terrace(b, 0.0, 0.012, 0.058, 0.225, 0.19, 0.2155, 0.1805, ZIG_BRICK, MUD)
	else:
		_zig_terrace(b, 0.0, 0.012, 0.072, 0.225, 0.19, 0.2132, 0.1782, ZIG_BRICK)
		_zig_terrace(b, 0.0, 0.084, 0.006, 0.2132, 0.1782, 0.2122, 0.1772, ZIG_BITUMEN)
		_zig_terrace(b, 0.0, 0.090, 0.032, 0.2122, 0.1772, 0.207, 0.172, ZIG_BRICK, ZIG_WALK)
		_zig_stripes(b, 0.0, 0.012, 0.11, 0.225, 0.19, 0.207, 0.172,
			[-0.18, -0.108, -0.036, 0.036, 0.108, 0.18], [-0.114, -0.038, 0.038, 0.114])
		_zig_stair(b, 0.29, 0.012, 0.172, 0.122, 0.19, 0.03, 7)
		_zig_side_flights(b)
		for sx in [-1.0, 1.0]:                       # gate pylons where the three flights meet, civ caps
			b.box(Vector3(sx * 0.052, 0.137, 0.155), Vector3(0.022, 0.03, 0.022), ZIG_BRICK_HI)
			b.tag = M7MeshBuilder.TAG_CIV
			b.box(Vector3(sx * 0.052, 0.155, 0.155), Vector3(0.026, 0.006, 0.026), Color.WHITE)
			b.tag = M7MeshBuilder.TAG_PLAIN
	# --- terrace 2: y 0.122 .. 0.202, set back (cz -0.025)
	if stage == 2:
		_zig_terrace(b, -0.025, 0.122, 0.04, 0.15, 0.12, 0.144, 0.114, ZIG_BRICK_MID, MUD)
	elif stage >= 3:
		_zig_terrace(b, -0.025, 0.122, 0.08, 0.15, 0.12, 0.138, 0.108, ZIG_BRICK_MID, ZIG_WALK)
		_zig_stripes(b, -0.025, 0.122, 0.08, 0.15, 0.12, 0.138, 0.108, [-0.105, -0.035, 0.035, 0.105], [-0.06, 0.0, 0.06])
		_zig_stair(b, 0.15, 0.122, 0.083, 0.202, 0.095, 0.026, 5)
		# --- terrace 3: y 0.202 .. 0.256 (cz -0.04)
		_zig_terrace(b, -0.04, 0.202, 0.054, 0.095, 0.075, 0.088, 0.068, ZIG_BRICK_HI, ZIG_WALK.lightened(0.04))
		_zig_stair(b, 0.078, 0.202, 0.028, 0.256, 0.035, 0.022, 4)
	# --- summit shrine (on terrace 3, z -0.09 .. -0.01) and fire altar
	if stage == 3:
		b.box(Vector3(0, 0.273, -0.05), Vector3(0.11, 0.034, 0.08), ZIG_LIME.darkened(0.08), MUD)
		b.box(Vector3(0, 0.272, -0.0085), Vector3(0.024, 0.03, 0.003), DARK)
	elif stage == 4:
		b.box(Vector3(0, 0.281, -0.05), Vector3(0.11, 0.05, 0.08), ZIG_LIME)
		b.tag = M7MeshBuilder.TAG_CIV
		b.box(Vector3(0, 0.299, -0.05), Vector3(0.114, 0.008, 0.084), Color.WHITE)          # frieze
		b.tag = M7MeshBuilder.TAG_PLAIN
		b.box(Vector3(0, 0.31, -0.05), Vector3(0.122, 0.008, 0.092), ZIG_LIME.darkened(0.12), ZIG_LIME)  # roof slab
		b.box(Vector3(0, 0.32, -0.05), Vector3(0.05, 0.012, 0.012), M7LooksKit.GOLD)        # crest
		b.quad(Vector3(-0.012, 0.257, -0.0085), Vector3(0.012, 0.257, -0.0085), Vector3(0.012, 0.29, -0.0085),
			Vector3(-0.012, 0.29, -0.0085), DARK, Vector3(0, 0.28, -0.05))                  # door
		b.prism(Vector3(0, 0.256, 0.009), 0.011, 0.014, 0.009, 6, STONE_DARK, DARK)          # altar bowl
		b.tag = M7MeshBuilder.TAG_EMISSIVE
		b.prism(Vector3(0, 0.265, 0.009), 0.010, 0.0, 0.018, 5, Color("#ff7a20"), Color("#ff7a20"))
		b.prism(Vector3(0.002, 0.265, 0.01), 0.006, 0.0, 0.012, 4, Color("#ffc040"), Color("#ffc040"))
		b.tag = M7MeshBuilder.TAG_PLAIN
	# --- construction gear: earth ramp up +X, brick stacks, scaffold, the owner's pennant
	if stage < 4:
		var rh := 0.07 if stage == 1 else 0.122
		var xf := 0.36 if stage == 1 else 0.42
		var xh := 0.2155 if stage == 1 else 0.207
		b.quad(Vector3(xf, 0.0, -0.035), Vector3(xf, 0.0, 0.035), Vector3(xh, rh, 0.035), Vector3(xh, rh, -0.035),
			EARTH.lightened(0.12), Vector3((xf + xh) * 0.5, -0.05, 0))
		for sz in [-0.035, 0.035]:
			b.tri(Vector3(xf, 0.0, sz), Vector3(xh, 0.0, sz), Vector3(xh, rh, sz), EARTH, Vector3((xf + xh) * 0.5, rh * 0.3, 0))
		for p in [Vector3(0.40, 0.0, 0.075), Vector3(0.40, 0.0, -0.075)]:
			_zig_bricks(b, p)
		var stacks: Array = [
			[Vector3(-0.12, 0.07, -0.08), Vector3(-0.03, 0.07, 0.06), Vector3(0.09, 0.07, -0.1), Vector3(0.15, 0.07, 0.05)],
			[Vector3(-0.07, 0.162, -0.085), Vector3(0.06, 0.162, -0.005), Vector3(0.17, 0.122, 0.12)],
			[Vector3(0.07, 0.256, -0.05), Vector3(-0.115, 0.202, -0.025)]][stage - 1]
		for p in stacks:
			_zig_bricks(b, p)
		if stage == 3:                                    # ladder from terrace 1 to terrace 2
			for sz in [-0.012, 0.012]:
				b.pole(Vector3(0.19, 0.122, sz), Vector3(0.135, 0.21, sz), 0.0025, WOOD_LIGHT)
			for r in 3:
				var q := Vector3(0.19, 0.122, 0).lerp(Vector3(0.135, 0.21, 0), 0.25 + 0.25 * r)
				b.pole(q + Vector3(0, 0, -0.012), q + Vector3(0, 0, 0.012), 0.002, WOOD)
		var sc: Array = [[0.222, 0.0, 0.187, 0.012, 0.11], [0.147, -0.025, 0.117, 0.122, 0.2], [0.062, -0.05, 0.047, 0.256, 0.33]][stage - 1]
		var hx: float = sc[0]
		var cz: float = sc[1]
		var hz: float = sc[2]
		var y0: float = sc[3]
		var y1: float = sc[4]
		for sx in [-1.0, 1.0]:
			for sz in [-1.0, 1.0]:
				b.pole(Vector3(sx * hx, y0, cz + sz * hz), Vector3(sx * hx, y1, cz + sz * hz), 0.0035, WOOD_LIGHT)
		for sz in [-1.0, 1.0]:
			b.pole(Vector3(-hx, y1 - 0.018, cz + sz * hz), Vector3(hx, y1 - 0.018, cz + sz * hz), 0.0028, WOOD)
		var pp := Vector3(-hx, y1, cz + hz)
		b.pole(pp, pp + Vector3(0, 0.06, 0), 0.003, WOOD)
		b.tag = M7MeshBuilder.TAG_CIV
		b.quad(pp + Vector3(0.002, 0.058, 0), pp + Vector3(0.05, 0.048, 0), pp + Vector3(0.05, 0.02, 0), pp + Vector3(0.002, 0.03, 0),
			Color.WHITE, pp + Vector3(0.02, 0.04, 0.3))
		b.tag = M7MeshBuilder.TAG_PLAIN
	return b.commit()


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
