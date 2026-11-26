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
##   field_cane      FOD-10: a sugar-cane field on a cane tile (the cane deposit marker already stands there): dense rows of tall green canes with feathery
##                   leaves and a cart waiting at the edge, fixed greens (a perennial, so it takes no seasonal tint); sugar has no building of its own
##   field_cane_cut  the same at harvest: the near rows cut to stubble, the cut canes tied in bundles on the ground and loaded on the cart, two cutters at work
##   fishing_bank    HNT-12: a patch of the sea 2 to 3 tiles from a harbor town that has fleets working it: a scatter of pale ripple rings on the water, a few
##                   small red-and-white buoys, and gulls wheeling over it; set at the water's surface and drawn only while a fleet fishes there
##   canal_nav_straight  TRN-11: one tile of navigation canal along X, flat-to-flat (0.84): a straight channel of dark water between stone-faced banks with
##                       a gravel towpath on the +Z side and a few mooring posts (the E3 irrigation canal is a ditch; this is wider, straight and faced)
##   canal_nav_lock      the same tile with a lock: higher stone chamber walls, a pair of mitre gates at each end with balance beams, a paddle post and
##                       a lock-keeper's bollards
##   toll_gate           TRN-10: a small prop across a road along X: a swing gate of white-barred timber across the road, an octagonal toll house with a
##                       bay window and a lamp beside it, a price board on a post; a road upgrade, drawn on an existing paved road
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
	kit.meshes["soot_stain"] = _soot_stain(kit)
	kit.meshes["field_cane"] = _cane_field(kit, false)
	kit.meshes["field_cane_cut"] = _cane_field(kit, true)
	kit.meshes["fishing_bank"] = _bank(kit)
	kit.meshes["canal_nav_straight"] = _canal(kit, false)
	kit.meshes["canal_nav_lock"] = _canal(kit, true)
	kit.meshes["toll_gate"] = _toll_gate(kit)

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

# ------------------------------------------------------------------ soot

const SOOT_DARK := Color("#26231f")
const SOOT_PALE := Color("#38332d")

## Soot (docs/MVP/39 §5, A-16): a warm grey-black stain on the ground, four overlapping blotches about one tile across at scale 1, flat on the ground. It is a local state
## beside a working factory or coke oven, scaled by the soot the town holds (`soot_raw` of the frame): the renderer sizes it, and it shrinks as the soot falls.
static func _soot_stain(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2095)
	b.tag = PLAIN
	for d in [[0.0, 0.0, 0.46, SOOT_DARK], [0.22, 0.12, 0.3, SOOT_PALE], [-0.2, -0.14, 0.28, SOOT_PALE], [0.06, -0.28, 0.22, SOOT_DARK]]:
		var r: float = d[2]
		b.prism(Vector3(float(d[0]), 0.002, float(d[1])), r, r * 0.96, 0.0015, 14, d[3], d[3])
	return b.commit()

# ------------------------------------------------------------------ navigation canal and turnpike

const BANK := Color("#8f8a80")
const BANK_LIGHT := Color("#b3ad9f")
const BANK_DARK := Color("#6c675f")
const CANAL_WATER := Color("#2f5873")
const CANAL_WATER_LIGHT := Color("#4a82a6")
const TOWPATH := Color("#bfae88")
const POST := Color("#5e4631")
const TIMBER_LIGHT := Color("#8a6a48")
const WHITE := Color("#efe9da")
const TILE := Color("#a8492c")
const TILE_DARK := Color("#7f3622")
const PLASTER := Color("#e7dcc2")
const DOOR := Color("#2a2119")
const LAMP := Color("#ffd27a")
const GLASS := Color("#39485a")
const BOARD := Color("#d8cdb0")
const IRON := Color("#4a4f57")
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE

const TILE_W := 0.84

static func _canal(kit, lock: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2040 if not lock else 2041)
	b.tag = PLAIN
	var half := TILE_W * 0.5
	var cw := 0.2            # the water's width (Z)
	# the towpath on the +Z side, the water, the stone-faced banks either side of it
	b.box(Vector3(0, 0.003, cw * 0.5 + 0.05), Vector3(TILE_W, 0.006, 0.08), TOWPATH)
	b.box(Vector3(0, 0.004, 0.0), Vector3(TILE_W, 0.008, cw + 0.03), BANK_DARK)
	b.box(Vector3(0, 0.0085, 0.0), Vector3(TILE_W, 0.002, cw), CANAL_WATER)
	for sz in [-1.0, 1.0]:
		var z: float = sz
		b.box(Vector3(0, 0.012, z * (cw * 0.5 + 0.007)), Vector3(TILE_W, 0.018, 0.014), BANK, BANK_LIGHT)
		# coping stones along each bank
		for k in 10:
			b.box(Vector3(-half + 0.04 + 0.084 * k, 0.0225, z * (cw * 0.5 + 0.007)), Vector3(0.08, 0.004, 0.018), BANK_LIGHT if k % 2 == 0 else BANK)
	# a few ripples of lighter water so it reads as moving
	for k in 5:
		b.box(Vector3(-0.28 + 0.13 * k, 0.0098, -0.03 + 0.04 * (k % 3)), Vector3(0.05, 0.001, 0.006), CANAL_WATER_LIGHT)
	# mooring posts on the towpath edge
	for k in 3:
		b.pole(Vector3(-0.3 + 0.3 * k, 0.014, cw * 0.5 + 0.022), Vector3(-0.3 + 0.3 * k, 0.04, cw * 0.5 + 0.022), 0.0042, POST, 5)
	if not lock:
		return b.commit()
	# the lock: the chamber's walls stand higher, with a gate pair at each end of the chamber (two leaves meeting in a point), each with a long balance beam
	for sz in [-1.0, 1.0]:
		var z2: float = sz
		b.box(Vector3(0, 0.026, z2 * (cw * 0.5 + 0.007)), Vector3(0.42, 0.03, 0.02), BANK, BANK_LIGHT)
		b.box(Vector3(0, 0.0425, z2 * (cw * 0.5 + 0.007)), Vector3(0.43, 0.004, 0.024), BANK_LIGHT)
	for ex in [-0.21, 0.21]:
		var x: float = ex
		var dir := -1.0 if x > 0.0 else 1.0     # the leaves close to a point that faces away from the chamber
		for sz in [-1.0, 1.0]:
			var z3: float = sz
			var a := Vector3(x, 0.0, z3 * cw * 0.5)
			var tip2 := Vector3(x - dir * 0.045, 0.0, 0.0)
			# a gate leaf: two timber rails and a body between them, from the hinge post on the chamber wall to the point of the V
			b.pole(a + Vector3(0, 0.014, 0), tip2 + Vector3(0, 0.014, 0), 0.0072, POST, 4)
			b.pole(a + Vector3(0, 0.032, 0), tip2 + Vector3(0, 0.032, 0), 0.0072, TIMBER_LIGHT, 4)
			b.pole(a + Vector3(0, 0.0, 0), a + Vector3(0, 0.045, 0), 0.006, BANK_DARK, 5)
			# the balance beam: a stout arm reaching back over the bank from the hinge
			b.pole(a + Vector3(0, 0.045, 0), a + Vector3(dir * 0.0, 0.045, z3 * 0.075) + Vector3(-dir * 0.03, 0.0, 0.0), 0.0045, POST, 4)
	# a paddle post and a lock-keeper's bollards on the towpath
	b.pole(Vector3(0.12, 0.03, cw * 0.5 + 0.03), Vector3(0.12, 0.07, cw * 0.5 + 0.03), 0.0036, IRON, 4)
	b.box(Vector3(0.12, 0.072, cw * 0.5 + 0.03), Vector3(0.012, 0.006, 0.006), IRON)
	for k in 2:
		b.prism(Vector3(-0.15 + 0.3 * k, 0.012, cw * 0.5 + 0.06), 0.008, 0.007, 0.014, 6, IRON, BANK_DARK)
	return b.commit()

static func _toll_gate(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2050)
	b.tag = PLAIN
	# the paved road through the gate along X
	b.box(Vector3(0, 0.002, 0.0), Vector3(0.34, 0.004, 0.07), Color("#a69f8a"))
	# the swing gate: a post at each side of the road, a barred timber gate shut across it, white with a diagonal brace
	for sx in [-1.0, 1.0]:
		var x: float = sx
		b.pole(Vector3(x * 0.04, 0.0, 0.04 * 0.0), Vector3(x * 0.04, 0.056, 0.0), 0.0046, POST, 4)
		b.blob(Vector3(x * 0.04, 0.059, 0.0), Vector3(0.0055, 0.0055, 0.0055), 4, 2, WHITE, 0.0)
	for k in 3:
		b.pole(Vector3(-0.036, 0.016 + 0.014 * k, 0.0), Vector3(0.036, 0.016 + 0.014 * k, 0.0), 0.0032, WHITE, 4)
	b.pole(Vector3(-0.036, 0.016, 0.0), Vector3(0.036, 0.044, 0.0), 0.0026, WHITE, 4)
	# the toll house beside the road: an octagonal plastered cabin with a bay window facing the road, a pitched tiled roof and a chimney
	var hp := Vector3(0.12, 0.0, 0.07)
	b.prism(hp, 0.034, 0.034, 0.046, 8, PLASTER, PLASTER.lightened(0.05), PI / 8.0)
	b.prism(hp + Vector3(0, 0.046, 0), 0.04, 0.0, 0.038, 8, TILE, TILE_DARK, PI / 8.0)
	b.prism(hp + Vector3(0.012, 0.07, 0.0), 0.006, 0.006, 0.026, 4, TILE_DARK, TILE_DARK, PI / 4.0)
	b.box(hp + Vector3(0, 0.022, 0.0315), Vector3(0.018, 0.022, 0.004), GLASS)
	b.box(hp + Vector3(-0.0245, 0.016, 0.0245), Vector3(0.012, 0.026, 0.004), DOOR)
	b.tag = EMISSIVE
	b.box(hp + Vector3(0, 0.022, 0.0335), Vector3(0.013, 0.016, 0.0015), LAMP)
	b.tag = PLAIN
	# a lamp on a bracket at the gate post and a price board on a post by the track
	b.pole(Vector3(0.04, 0.048, 0.0), Vector3(0.04, 0.048, -0.016), 0.0016, IRON, 4)
	b.box(Vector3(0.04, 0.044, -0.018), Vector3(0.008, 0.01, 0.008), IRON)
	b.tag = EMISSIVE
	b.box(Vector3(0.04, 0.044, -0.018), Vector3(0.0055, 0.0075, 0.0055), LAMP)
	b.tag = PLAIN
	b.pole(Vector3(-0.1, 0.0, -0.05), Vector3(-0.1, 0.06, -0.05), 0.0026, POST, 4)
	b.box(Vector3(-0.1, 0.066, -0.05), Vector3(0.05, 0.034, 0.004), BOARD)
	for k in 3:
		b.box(Vector3(-0.1, 0.075 - 0.0095 * k, -0.0475), Vector3(0.036 - 0.004 * k, 0.003, 0.0015), IRON)
	return b.commit()

# ------------------------------------------------------------------ sugar cane

const CANE := Color("#6aa23c")
const CANE_DARK := Color("#4e8a2c")
const CANE_LIGHT := Color("#8cc454")
const CANE_STUBBLE := Color("#a5a05a")
const SOIL_WET := Color("#6a4a30")
const BUOY_RED := Color("#c9342a")
const GULL := Color("#f4f0e6")
const RIPPLE := Color("#dff0f8")

static func _cane_row(b: M7MeshBuilder, x0: float, x1: float, z: float, h: float, cut: bool) -> void:
	if cut:
		b.box(Vector3((x0 + x1) * 0.5, 0.01, z), Vector3(x1 - x0, 0.012, 0.022), CANE_STUBBLE, CANE_STUBBLE.lightened(0.08))
		return
	# a dense hedge-like block of canes, a lighter plume of leaves above it at intervals
	b.box(Vector3((x0 + x1) * 0.5, h * 0.5, z), Vector3(x1 - x0, h, 0.02), CANE_DARK, CANE)
	var n := maxi(3, int((x1 - x0) / 0.06))
	for k in n:
		var cx := lerpf(x0, x1, (float(k) + 0.5) / n)
		b.blob(Vector3(cx, h + 0.008, z), Vector3(0.03, 0.016, 0.016), 5, 2, CANE_LIGHT if k % 2 == 0 else CANE, 0.2, CANE_DARK)

static func _cane_field(kit, cut: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2060 if not cut else 2061)
	var fr := M7WorldData.R * 0.84
	b.tag = PLAIN
	# the soil over the hex, dark and wet
	var up := Vector3(0, -1, 0)
	var ring: Array = M7LooksKit._hex_ring(fr)
	for i in 6:
		var p: Vector2 = ring[i]
		var q: Vector2 = ring[(i + 1) % 6]
		b.tri(Vector3(0, 0.004, 0), Vector3(p.x, 0.004, p.y), Vector3(q.x, 0.004, q.y), SOIL_WET, up)
	# rows running along X, tall in the back half and (when cutting) stubble in the near rows
	var rows := 7
	for r in rows:
		var z: float = lerpf(-fr * 0.82, fr * 0.78, float(r) / (rows - 1))
		var w := M7MeshKit._hex_half_width(z, fr) - 0.045
		if w < 0.06:
			continue
		var is_cut := cut and z > -fr * 0.1
		_cane_row(b, -w, w, z, 0.058 + 0.008 * (r % 2), is_cut)
	if cut:
		# the cut canes tied in bundles lying between the stubble rows, and a pair of cutters
		for k in 5:
			var bx := -0.2 + 0.09 * k
			b.box(Vector3(bx, 0.016, fr * 0.2 + 0.03 * (k % 2)), Vector3(0.07, 0.014, 0.014), CANE, CANE_LIGHT)
			b.pole(Vector3(bx - 0.012, 0.018, fr * 0.2 + 0.03 * (k % 2) - 0.008), Vector3(bx - 0.012, 0.018, fr * 0.2 + 0.03 * (k % 2) + 0.008), 0.0016, ROPE_COL, 3)
		for sx in [-0.12, 0.1]:
			var x: float = sx
			_cutter(b, Vector3(x, 0.006, fr * 0.42))
	# the cart at the field's edge: a flat bed on two wheels with a shaft, loaded with bundles when cutting
	var cx2 := -fr * 0.62
	b.box(Vector3(cx2, 0.026, fr * 0.1), Vector3(0.05, 0.006, 0.09), WOOD, WOOD_LIGHT)
	for sz in [-0.04, 0.04]:
		var z2: float = sz
		for sx in [-1.0, 1.0]:
			var x2: float = sx
			b.prism(Vector3(cx2 + x2 * 0.027, 0.018, fr * 0.1 + z2), 0.014, 0.014, 0.004, 8, WOOD, WOOD_LIGHT)
	b.pole(Vector3(cx2, 0.026, fr * 0.1 + 0.045), Vector3(cx2, 0.03, fr * 0.1 + 0.11), 0.0026, WOOD, 4)
	if cut:
		for k in 3:
			b.box(Vector3(cx2, 0.036 + 0.011 * k, fr * 0.1), Vector3(0.04, 0.011, 0.08 - 0.01 * k), CANE, CANE_LIGHT)
	# corner stones as the other field meshes have
	for i in 6:
		var corner: Vector2 = M7LooksKit._hex_ring(fr * 0.97)[i]
		b.blob(Vector3(corner.x, 0.008, corner.y), Vector3(0.012, 0.01, 0.011), 5, 3, M7MeshKit.STONE, 0.1, M7MeshKit.STONE_DARK)
	return b.commit()

const ROPE_COL := Color("#c8b88a")
const WOOD_LIGHT := Color("#8a6a48")
const CIV := M7MeshBuilder.TAG_CIV

static func _cutter(b: M7MeshBuilder, p: Vector3) -> void:
	# a stooping cutter a tenth tall with a straw hat and a long machete: a coat in the owner's colour on the shoulders, pale trousers, a bare forearm
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis(Vector3.RIGHT, 0.5), p)
	b.prism(Vector3(-0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, Color("#d8d2c0"), Color("#d8d2c0"))
	b.prism(Vector3(0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, Color("#d8d2c0"), Color("#d8d2c0"))
	b.prism(Vector3(0, 0.034, 0), 0.0185, 0.0155, 0.036, 7, Color("#a8a090"), Color("#a8a090"))
	b.tag = CIV
	b.prism(Vector3(0, 0.054, 0), 0.0205, 0.009, 0.014, 7, Color.WHITE, Color.WHITE)
	b.tag = PLAIN
	b.blob(Vector3(0, 0.084, 0), Vector3(0.0125, 0.013, 0.0125), 7, 4, Color("#b07a52"), 0.0)
	b.prism(Vector3(0, 0.091, 0), 0.0215, 0.02, 0.002, 9, Color("#d8c27a"), Color("#b8a45a"))
	b.blob(Vector3(0, 0.095, 0), Vector3(0.0115, 0.007, 0.0115), 6, 3, Color("#d8c27a"), 0.0)
	b.pole(Vector3(0.02, 0.06, 0), Vector3(0.026, 0.044, 0.016), 0.0045, Color("#b07a52"))
	b.pole(Vector3(0.026, 0.044, 0.016), Vector3(0.03, 0.022, 0.034), 0.0021, Color("#8d949c"), 3)
	b.xf = save

# ------------------------------------------------------------------ the fishing bank

static func _bank(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2070)
	b.tag = PLAIN
	# pale ripple rings spread on the water: flat annuli in the XZ plane at the surface, thin and a little irregular
	var rings := [[0.0, 0.0, 0.085, 0.007], [0.13, 0.07, 0.06, 0.006], [-0.14, 0.05, 0.07, 0.006], [0.02, -0.15, 0.065, 0.006], [-0.07, -0.08, 0.045, 0.005], [0.12, -0.1, 0.05, 0.005]]
	for rg in rings:
		var cx: float = rg[0]
		var cz: float = rg[1]
		var rr: float = rg[2]
		var th: float = rg[3]
		for i in 14:
			var a0 := TAU * i / 14.0
			var a1 := TAU * (i + 1) / 14.0
			if i % 5 == 4:
				continue         # a gap in the ring so it reads as a ripple, not a hoop
			var o0 := Vector3(cx + cos(a0) * rr, 0.004, cz + sin(a0) * rr)
			var o1 := Vector3(cx + cos(a1) * rr, 0.004, cz + sin(a1) * rr)
			var i0 := Vector3(cx + cos(a0) * (rr - th), 0.004, cz + sin(a0) * (rr - th))
			var i1 := Vector3(cx + cos(a1) * (rr - th), 0.004, cz + sin(a1) * (rr - th))
			b.quad(o0, o1, i1, i0, RIPPLE, Vector3(cx, -1.0, cz))
	# three buoys: a short pole with a red-and-white float and a flag-less top mark
	for bp in [[-0.06, 0.1], [0.1, -0.04], [-0.1, -0.12]]:
		var bx: float = bp[0]
		var bz: float = bp[1]
		b.prism(Vector3(bx, 0.0, bz), 0.011, 0.009, 0.016, 7, BUOY_RED, BUOY_RED.lightened(0.1))
		b.prism(Vector3(bx, 0.008, bz), 0.0115, 0.0115, 0.004, 7, GULL, GULL)
		b.pole(Vector3(bx, 0.016, bz), Vector3(bx, 0.038, bz), 0.0017, Color("#4a3726"), 3)
		b.blob(Vector3(bx, 0.04, bz), Vector3(0.004, 0.004, 0.004), 4, 2, BUOY_RED, 0.0)
	# gulls: each a body with two swept wings, hovering at different heights over the patch
	for gp in [[0.0, 0.1, 0.0, 0.5], [0.09, 0.14, 0.08, -0.4], [-0.1, 0.12, -0.03, 1.2], [0.04, 0.16, -0.12, 2.1]]:
		var gx: float = gp[0]
		var gy: float = gp[1]
		var gz: float = gp[2]
		var yaw: float = gp[3]
		var d := Vector3(cos(yaw), 0, sin(yaw))
		var sd := Vector3(-d.z, 0, d.x)
		var c := Vector3(gx, gy, gz)
		b.blob(c, Vector3(0.0095, 0.0045, 0.0055), 5, 2, GULL, 0.0)
		b.tri(c + sd * 0.004, c + sd * 0.03 - d * 0.008 + Vector3(0, 0.006, 0), c + sd * 0.012 - d * 0.012, GULL, c + Vector3(0, -1.0, 0))
		b.tri(c - sd * 0.004, c - sd * 0.03 - d * 0.008 + Vector3(0, 0.006, 0), c - sd * 0.012 - d * 0.012, GULL, c + Vector3(0, -1.0, 0))
		b.tri(c + sd * 0.004, c + sd * 0.03 - d * 0.008 + Vector3(0, 0.006, 0), c + sd * 0.012 - d * 0.012, GULL.darkened(0.12), c + Vector3(0, 1.0, 0))
		b.tri(c - sd * 0.004, c - sd * 0.03 - d * 0.008 + Vector3(0, 0.006, 0), c - sd * 0.012 - d * 0.012, GULL.darkened(0.12), c + Vector3(0, 1.0, 0))
	return b.commit()
