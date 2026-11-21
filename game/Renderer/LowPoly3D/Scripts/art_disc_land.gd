## Discovery (E6) land: the four-course rotation and enclosure (AGR-12), docs/MVP/39 §5.
##
## Not wired to the simulation yet (docs/MVP/39 §9: `LOOK_ENCLOSE` says a civilization has AGR-12 and a `fallowTiles`-style phase byte says where each field
## stands in the rotation); photographed in the Discovery gallery (LowPoly3DView.show_discovery_gallery). Same footprint and conventions as the field meshes in
## looks_kit.gd and art_fief_land.gd (a pointy-top hex, local radius R * 0.84, the builder scales and tints it like `field`). The four-course field has NO
## FALLOW: four bands, each a different crop, and the whole field is ENCLOSED by a hedge, with a hedge between every pair of bands and a gate in the front.
##
##   field_four_0..3   the rotation in its four phases; the four kinds always lie in the order wheat, turnips, barley, clover, shifted one band per phase:
##                     wheat       tall ridged rows (TAG_CROP, takes the season's colour)
##                     turnips     rows of dark leafy tufts with pale round roots half out of the ground (the fodder that feeds the winter herds)
##                     barley      closer, finer rows (TAG_CROP), told from the wheat by the tight spacing and a paler soil between them
##                     clover      a low pink-flowered meadow with three sheep folded on it (the nitrogen the next wheat needs)
##   hedge_line        a straight run of hedge 0.3 long along X for edges the field mesh does not cover, with a few small trees in it
extends RefCounted

const SOIL := Color("#7a5a3c")
const SOIL_PALE := Color("#9a7c54")
const LEAF := Color("#3f7a34")
const LEAF_DARK := Color("#2f6228")
const ROOT := Color("#e6dcc8")
const ROOT_TOP := Color("#a7608f")
const CLOVER := Color("#6a9a3e")
const CLOVER_DARK := Color("#527e30")
const BLOOM := Color("#d77aa8")
const HEDGE := Color("#3d6e2c")
const HEDGE_DARK := Color("#2f5822")
const HEDGE_LIGHT := Color("#5a8a3a")
const WOOD := Color("#5e4631")
const WOOL := Color("#ece5d6")
const SHEEP_FACE := Color("#4a3a2c")
const STONE := Color("#8f8a80")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CROP := M7MeshBuilder.TAG_CROP
const FOLIAGE := M7MeshBuilder.TAG_FOLIAGE

const WHEAT := 0
const TURNIP := 1
const BARLEY := 2
const CLOVER_KIND := 3

static func build(kit) -> void:
	for phase in 4:
		var kinds := []
		for band in 4:
			kinds.append((band + phase) % 4)
		kit.meshes["field_four_%d" % phase] = _field(kit, kinds, 2001 + phase)
	kit.meshes["hedge_line"] = _hedge_line(kit)

## A hedge from p to q: a low bank in short square-section runs of two greens (a broad foot and a narrower, lighter crown), thickness `t`, height `h`; `gap`
## leaves a gate opening of that fraction in the middle. Cheap on purpose: a field is drawn on every farmed tile.
static func _hedge(b: M7MeshBuilder, p: Vector3, q: Vector3, h: float, t: float, gap: float = 0.0) -> void:
	var d := q - p
	var len := d.length()
	var n := maxi(2, int(len / 0.07))
	var save := b.tag
	b.tag = FOLIAGE
	for k in n:
		var u0 := float(k) / n
		var u1 := float(k + 1) / n
		var um := (u0 + u1) * 0.5
		if gap > 0.0 and absf(um - 0.5) < gap * 0.5:
			continue
		var a := p + d * (u0 + 0.01)
		var e := p + d * (u1 - 0.01)
		var foot: Color = HEDGE if k % 2 == 0 else HEDGE_DARK
		b.pole(a + Vector3(0, h * 0.3, 0), e + Vector3(0, h * 0.3, 0), t * 0.95, foot, 4)
		b.pole(a + Vector3(0, h * 0.72, 0), e + Vector3(0, h * 0.72, 0), t * 0.62, HEDGE_LIGHT if k % 3 == 0 else HEDGE, 4)
	b.tag = save
	# gate posts either side of the gap
	if gap > 0.0:
		for s in [-1.0, 1.0]:
			var x: float = s
			var gp := p + d * (0.5 + x * gap * 0.5)
			b.pole(gp, gp + Vector3(0, h * 0.8, 0), 0.0045, WOOD, 4)

static func _sheep(b: M7MeshBuilder, p: Vector3, yaw: float) -> void:
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis(Vector3.UP, yaw).scaled(Vector3.ONE * 2.0), p)
	b.blob(Vector3(0, 0.014, 0), Vector3(0.0105, 0.0095, 0.0155), 6, 3, WOOL, 0.08, WOOL.darkened(0.12))
	b.blob(Vector3(0, 0.0175, 0.0165), Vector3(0.0055, 0.0055, 0.0065), 5, 2, SHEEP_FACE, 0.0)
	for sx in [-1.0, 1.0]:
		var x: float = sx
		for sz in [-0.008, 0.008]:
			var z: float = sz
			b.pole(Vector3(x * 0.0055, 0.007, z), Vector3(x * 0.0055, 0.0, z), 0.0012, SHEEP_FACE, 3)
	b.xf = save

## One band of the field between z0 and z1: floor, contents and kind. Same geometry rules as art_fief_land.gd's `_band`.
static func _band(b: M7MeshBuilder, fr: float, z0: float, z1: float, kind: int, band: int) -> void:
	var hw := M7MeshKit._hex_half_width(0.0, fr)
	var col := SOIL
	if kind == BARLEY:
		col = SOIL_PALE
	elif kind == TURNIP:
		col = SOIL.darkened(0.12)
	elif kind == CLOVER_KIND:
		col = CLOVER.darkened(0.1)
	b.tag = FOLIAGE if kind == CLOVER_KIND else PLAIN
	var up := Vector3(0, -1, 0)
	if band == 1 or band == 2:
		b.quad(Vector3(-hw, 0.004, z0), Vector3(hw, 0.004, z0), Vector3(hw, 0.004, z1), Vector3(-hw, 0.004, z1), col, up)
	else:
		var tip := Vector3(0, 0.004, z0 if band == 0 else z1)
		var cut := z1 if band == 0 else z0
		var sh := -fr * 0.5 if band == 0 else fr * 0.5
		b.tri(tip, Vector3(hw, 0.004, sh), Vector3(-hw, 0.004, sh), col, up)
		b.quad(Vector3(-hw, 0.004, sh), Vector3(hw, 0.004, sh), Vector3(hw, 0.004, cut), Vector3(-hw, 0.004, cut), col, up)
	b.tag = PLAIN
	var zc := (z0 + z1) * 0.5
	if kind == WHEAT or kind == BARLEY:
		b.tag = CROP
		var rows := 4 if kind == WHEAT else 7
		var hh := 0.019 if kind == WHEAT else 0.012
		var wd := 0.014 if kind == WHEAT else 0.007
		for r in rows:
			var z: float = lerpf(maxf(z0, -fr * 0.9), minf(z1, fr * 0.9), (r + 0.5) / rows)
			var w := M7MeshKit._hex_half_width(z, fr) - 0.035
			if w < 0.05:
				continue
			var a0 := Vector3(-w, 0.004, z - wd)
			var a1 := Vector3(w, 0.004, z - wd)
			var c0 := Vector3(-w, 0.004 + hh, z)
			var c1 := Vector3(w, 0.004 + hh, z)
			var d0 := Vector3(-w, 0.004, z + wd)
			var d1 := Vector3(w, 0.004, z + wd)
			b.quad(a0, a1, c1, c0, SOIL.lightened(0.05), Vector3(0, 0, z + 0.05))
			b.quad(d0, d1, c1, c0, SOIL.darkened(0.05), Vector3(0, 0, z - 0.05))
			b.tri(a0, c0, d0, SOIL, Vector3(0.0, 0.0, z))
			b.tri(a1, c1, d1, SOIL, Vector3(0.0, 0.0, z))
	elif kind == TURNIP:
		var rows2 := 3
		for r in rows2:
			var z2: float = lerpf(maxf(z0, -fr * 0.85), minf(z1, fr * 0.85), (r + 0.5) / rows2)
			var w2 := M7MeshKit._hex_half_width(z2, fr) - 0.05
			if w2 < 0.06:
				continue
			var n := maxi(4, int(w2 * 2.0 / 0.075))
			for k in n:
				var x := lerpf(-w2 * 0.92, w2 * 0.92, (k + 0.5) / n)
				b.blob(Vector3(x, 0.007, z2), Vector3(0.0085, 0.0075, 0.0085), 5, 2, ROOT, 0.04, ROOT_TOP)
				b.tag = FOLIAGE
				b.blob(Vector3(x, 0.016, z2), Vector3(0.011, 0.0075, 0.011), 5, 2, LEAF if (k + r) % 2 == 0 else LEAF_DARK, 0.14)
				b.tag = PLAIN
	else:
		# the clover meadow: low pads of green with pink blooms, three sheep folded on it
		b.tag = FOLIAGE
		for i in 6:
			var px := b.rnds() * (hw - 0.1)
			var pz: float = lerpf(maxf(z0, -fr * 0.8), minf(z1, fr * 0.8), 0.12 + 0.76 * b.rnd())
			if absf(px) > M7MeshKit._hex_half_width(pz, fr) - 0.08:
				continue
			b.blob(Vector3(px, 0.008, pz), Vector3(0.05 + 0.03 * b.rnd(), 0.013, 0.035 + 0.025 * b.rnd()), 6, 2, CLOVER if i % 2 == 0 else CLOVER_DARK, 0.1)
		b.tag = PLAIN
		for i in 8:
			var bx := b.rnds() * (hw - 0.1)
			var bz: float = lerpf(maxf(z0, -fr * 0.8), minf(z1, fr * 0.8), 0.1 + 0.8 * b.rnd())
			if absf(bx) > M7MeshKit._hex_half_width(bz, fr) - 0.07:
				continue
			b.blob(Vector3(bx, 0.018, bz), Vector3(0.0065, 0.0045, 0.0065), 4, 2, BLOOM, 0.1)
		var spread := minf(0.2, M7MeshKit._hex_half_width(zc, fr) - 0.13)
		_sheep(b, Vector3(-spread, 0.006, zc - 0.015), 0.4)
		_sheep(b, Vector3(spread * 0.1, 0.006, zc + 0.03), -0.9)
		_sheep(b, Vector3(spread * 0.9, 0.006, zc - 0.03), 1.7)
		# a hurdle fold: a short fence of woven panels round them
		for k in 4:
			var fx := -spread - 0.03 + 0.04 * k
			b.pole(Vector3(fx, 0.0, zc - 0.058), Vector3(fx, 0.02, zc - 0.058), 0.0018, WOOD, 3)
		b.pole(Vector3(-spread - 0.03, 0.012, zc - 0.058), Vector3(-spread + 0.09, 0.012, zc - 0.058), 0.0016, WOOD, 3)
	b.tag = PLAIN

static func _field(kit, kinds: Array, seed: int) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(seed)
	var fr := M7WorldData.R * 0.84
	var edges := [-fr, -fr / 2.0, 0.0, fr / 2.0, fr]
	for band in 4:
		_band(b, fr, float(edges[band]), float(edges[band + 1]), int(kinds[band]), band)
	# the hedges between the bands: a hedge across the field at each boundary, with a gate gap in the middle
	for z in [-fr / 2.0, 0.0, fr / 2.0]:
		var zz: float = z
		var w := M7MeshKit._hex_half_width(zz, fr)
		_hedge(b, Vector3(-w, 0.0, zz), Vector3(w, 0.0, zz), 0.028, 0.012, 0.18)
	# and the enclosing hedge round the whole hex, a gate gap at the front corner pair so the field can be entered
	var ring: Array = M7LooksKit._hex_ring(fr * 0.97)
	for i in 6:
		var p: Vector2 = ring[i]
		var q: Vector2 = ring[(i + 1) % 6]
		_hedge(b, Vector3(p.x, 0.0, p.y), Vector3(q.x, 0.0, q.y), 0.034, 0.014, 0.22 if i == 1 else 0.0)
	# a stone gate pier pair at the front gap, and a field tree or two grown in the hedge
	b.tag = PLAIN
	var gz := fr * 0.78
	b.blob(Vector3(-0.014, 0.012, gz), Vector3(0.007, 0.012, 0.007), 5, 3, STONE, 0.1, STONE)
	b.blob(Vector3(0.014, 0.012, gz), Vector3(0.007, 0.012, 0.007), 5, 3, STONE, 0.1, STONE)
	return b.commit()

static func _hedge_line(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2010)
	_hedge(b, Vector3(-0.15, 0.0, 0.0), Vector3(0.15, 0.0, 0.0), 0.04, 0.016, 0.0)
	# a small tree grown up through it, and a stile
	b.tag = PLAIN
	b.pole(Vector3(0.05, 0.0, 0.0), Vector3(0.05, 0.05, 0.0), 0.0045, WOOD, 5)
	b.tag = FOLIAGE
	b.blob(Vector3(0.05, 0.07, 0.0), Vector3(0.026, 0.03, 0.026), 6, 3, HEDGE_LIGHT, 0.15, HEDGE)
	b.tag = PLAIN
	return b.commit()
