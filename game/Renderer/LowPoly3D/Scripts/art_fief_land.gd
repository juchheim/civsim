## Faith & Fief (E5) land: three-field rotation (AGR-11), docs/MVP/32 §5.
##
## Not wired to the simulation yet (docs/MVP/32 §9: `fallowTiles` carries the rotation phase per field tile); photographed in the Fief gallery
## (LowPoly3DView.show_fief_gallery). Same footprint and conventions as the field meshes in looks_kit.gd (a pointy-top hex, local radius R * 0.84, the
## builder scales and tints it like `field`). One third of the field always lies fallow and grazed, and the strips turn each year:
##
##   field_three_0   fallow in the back strip (-Z), legumes in the middle, crop in the front
##   field_three_1   crop in the back, fallow in the middle, legumes in the front
##   field_three_2   legumes in the back, crop in the middle, fallow in the front
##
## The crop strip has ridges (TAG_CROP, takes the season's colour), the legume strip low bushy rows with pale flowers (its own green, so it is told
## from the crop at a glance), the fallow strip grass with weeds, dung and two cattle. Low grass balks divide the strips.
extends RefCounted

const SOIL := Color("#7a5a3c")
const GRASS := Color("#8ea548")
const DRYGRASS := Color("#b8a656")
const LEGUME := Color("#5f9a3a")
const LEGUME_DARK := Color("#4a7e2e")
const FLOWER := Color("#f1e6ee")
const WOOD := Color("#5e4631")
const COW_BROWN := Color("#7a4e2e")
const COW_PALE := Color("#e8dfce")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CROP := M7MeshBuilder.TAG_CROP
const FOLIAGE := M7MeshBuilder.TAG_FOLIAGE
const GRASS_TAG := M7MeshBuilder.TAG_GRASS

const KIND_CROP := 0
const KIND_LEGUME := 1
const KIND_FALLOW := 2

static func build(kit) -> void:
	# which strip kind lies in the back, middle and front band for each phase
	kit.meshes["field_three_0"] = _field(kit, [KIND_FALLOW, KIND_LEGUME, KIND_CROP], 1401)
	kit.meshes["field_three_1"] = _field(kit, [KIND_CROP, KIND_FALLOW, KIND_LEGUME], 1402)
	kit.meshes["field_three_2"] = _field(kit, [KIND_LEGUME, KIND_CROP, KIND_FALLOW], 1403)

## A cow standing at p, facing `yaw`: a barrel body with a pale patch, a head with horns, four legs and a tail.
static func _cow(b: M7MeshBuilder, p: Vector3, yaw: float, patch: bool) -> void:
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis(Vector3.UP, yaw).scaled(Vector3.ONE * 2.1), p)   # big enough to read at field scale
	var body: Color = COW_PALE if patch else COW_BROWN
	b.blob(Vector3(0, 0.022, 0), Vector3(0.012, 0.011, 0.02), 6, 3, body, 0.05, body.darkened(0.12))
	b.blob(Vector3(0.0, 0.026, -0.004), Vector3(0.0125, 0.007, 0.011), 5, 2, COW_BROWN if patch else COW_PALE, 0.05)
	b.box(Vector3(0, 0.026, 0.022), Vector3(0.011, 0.012, 0.012), body)
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(sx * 0.0045, 0.03, 0.028), Vector3(sx * 0.0075, 0.034, 0.03), 0.0011, COW_PALE)
		for sz in [-0.011, 0.011]:
			b.pole(Vector3(sx * 0.006, 0.014, sz), Vector3(sx * 0.006, 0.0, sz), 0.0016, body.darkened(0.2))
	b.pole(Vector3(0, 0.026, -0.019), Vector3(0.002, 0.014, -0.023), 0.001, COW_BROWN)
	b.xf = save

## One band of the field between z0 and z1: its floor (a quad in the band's colour, a pentagon at the ends where the hex narrows) and its contents.
static func _band(b: M7MeshBuilder, fr: float, z0: float, z1: float, kind: int, band: int) -> void:
	var hw := M7MeshKit._hex_half_width(0.0, fr)
	var col := SOIL
	var tag := PLAIN
	if kind == KIND_LEGUME:
		col = SOIL.darkened(0.14)
	elif kind == KIND_FALLOW:
		col = GRASS.lerp(DRYGRASS, 0.25)
		tag = GRASS_TAG
	b.tag = tag
	var up := Vector3(0, -1, 0)
	if band == 1:
		b.quad(Vector3(-hw, 0.004, z0), Vector3(hw, 0.004, z0), Vector3(hw, 0.004, z1), Vector3(-hw, 0.004, z1), col, up)
	else:
		# the end bands are pentagons: the hex's point, its two shoulders at z = +-fr / 2, then the straight cut across
		var tip := Vector3(0, 0.004, z0 if band == 0 else z1)
		var cut := z1 if band == 0 else z0
		var sh := -fr * 0.5 if band == 0 else fr * 0.5
		b.tri(tip, Vector3(hw, 0.004, sh), Vector3(-hw, 0.004, sh), col, up)
		b.quad(Vector3(-hw, 0.004, sh), Vector3(hw, 0.004, sh), Vector3(hw, 0.004, cut), Vector3(-hw, 0.004, cut), col, up)
	var zc := (z0 + z1) * 0.5
	if kind == KIND_CROP:
		b.tag = CROP
		var rows := 5
		for r in rows:
			var z: float = lerpf(maxf(z0, -fr * 0.9), minf(z1, fr * 0.9), (r + 0.5) / rows)
			var w := M7MeshKit._hex_half_width(z, fr) - 0.03
			if w < 0.05:
				continue
			var hh := 0.016
			var wd := 0.013
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
	elif kind == KIND_LEGUME:
		var rows2 := 4
		for r in rows2:
			var z2: float = lerpf(maxf(z0, -fr * 0.85), minf(z1, fr * 0.85), (r + 0.5) / rows2)
			var w2 := M7MeshKit._hex_half_width(z2, fr) - 0.045
			if w2 < 0.06:
				continue
			b.tag = FOLIAGE
			b.box(Vector3(0, 0.02, z2), Vector3(w2 * 2.0, 0.034, 0.028), LEGUME if r % 2 == 0 else LEGUME_DARK, LEGUME.lightened(0.1))
			b.tag = PLAIN
			var n := maxi(3, int(w2 * 2.0 / 0.07))
			for k in n:
				b.blob(Vector3(lerpf(-w2 * 0.9, w2 * 0.9, (k + 0.5) / n), 0.04, z2 + 0.004 * (k % 2)), Vector3(0.008, 0.005, 0.007), 4, 2, FLOWER, 0.1)
	else:
		# grass tufts, a few weeds and dung heaps, two cattle grazing
		b.tag = GRASS_TAG
		for i in 7:
			var px := b.rnds() * (hw - 0.1)
			var pz: float = lerpf(maxf(z0, -fr * 0.8), minf(z1, fr * 0.8), 0.15 + 0.7 * b.rnd())
			if absf(px) > M7MeshKit._hex_half_width(pz, fr) - 0.08:
				continue
			b.blob(Vector3(px, 0.006, pz), Vector3(0.05 + 0.04 * b.rnd(), 0.012, 0.035 + 0.03 * b.rnd()), 6, 2, GRASS.lerp(DRYGRASS, b.rnd()), 0.1)
		b.tag = PLAIN
		for i in 4:
			var dx := -0.2 + 0.13 * i + 0.03 * b.rnds()
			var dz: float = zc + 0.06 * b.rnds()
			if absf(dx) > M7MeshKit._hex_half_width(dz, fr) - 0.07:
				continue
			b.blob(Vector3(dx, 0.006, dz), Vector3(0.016, 0.008, 0.014), 5, 2, Color("#4a3626"), 0.12, Color("#3a2a1c"))
		var spread := minf(0.18, M7MeshKit._hex_half_width(zc, fr) - 0.12)
		_cow(b, Vector3(-spread, 0.004, zc - 0.02), 0.5, false)
		_cow(b, Vector3(spread * 0.6, 0.004, zc + 0.03), -1.1, true)
	b.tag = PLAIN

static func _field(kit, kinds: Array, seed: int) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(seed)
	var fr := M7WorldData.R * 0.84
	var edges := [-fr, -fr / 3.0, fr / 3.0, fr]
	for band in 3:
		_band(b, fr, float(edges[band]), float(edges[band + 1]), int(kinds[band]), band)
	# the balks: a low grass bank across the field between the strips
	b.tag = GRASS_TAG
	for z in [-fr / 3.0, fr / 3.0]:
		var w := M7MeshKit._hex_half_width(z, fr)
		b.box(Vector3(0, 0.008, z), Vector3(w * 2.0, 0.008, 0.022), GRASS.darkened(0.08), GRASS)
	b.tag = PLAIN
	# stones at the corners, as the other field meshes have
	for i in 6:
		var corner: Vector2 = M7LooksKit._hex_ring(fr * 0.97)[i]
		b.blob(Vector3(corner.x, 0.008, corner.y), Vector3(0.012, 0.01, 0.011), 5, 3, M7MeshKit.STONE, 0.1, M7MeshKit.STONE_DARK)
	return b.commit()
