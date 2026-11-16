## Faith & Fief (E5) ships: the cog (SEA-10 Keeled Ships & Cogs) and the cog with the lateen sail and sternpost rudder (SEA-11), docs/MVP/32 §7.
##
## Not wired to the simulation yet (docs/MVP/32 §9: a `LOOK_*` bit per civilization chooses the variant, as for the galley); photographed in the Fief
## gallery (LowPoly3DView.show_fief_gallery). Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.1 tall). Ships lie along Z with
## the bow toward +Z, like the galley; TAG_CIV faces take the owner's colour. Cargo doubles with the cog (SEA-10), so a cog carries visibly more
## than a galley, and it is only a ship on routes that already exist (no open-sea art in E5, docs/MVP/32 §12).
##
##   cog          a broad, deep, clinker-planked hull with straight raked posts, a raised aftercastle and a small forecastle, one mast with a great square
##                sail in the owner's colour (a pale sail with a coloured stripe), a crow's nest, a steering oar over the starboard quarter, and a deck of
##                barrels, bales and sacks
##   cog_lateen   SEA-11: the same cog with a sternpost rudder hung on the stern post (the steering oar gone) and a second, triangular lateen sail on a
##                mizzen mast over the aftercastle
extends RefCounted

const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const PLANK_LOW := Color("#7a5530")
const PLANK_MID := Color("#9a7040")
const PLANK_UP := Color("#b88a56")
const DECK := Color("#c9a56a")
const PITCH := Color("#2c2118")
const SAIL_CREAM := Color("#e8dcc0")
const ROPE := Color("#b9a68a")
const BALE := Color("#cdbf9f")
const SACK := Color("#d6c7a3")
const IRON := Color("#6e6a66")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV

const LENGTH := 0.5
const BEAM := 0.15
const DEPTH := 0.06
const HALF := LENGTH * 0.5

static func build(kit) -> void:
	kit.meshes["cog"] = _cog(kit, false)
	kit.meshes["cog_lateen"] = _cog(kit, true)

## The hull's breadth at u (0 at the stern, 1 at the bow): full-bodied amidships, blunt at the ends.
static func _hw(u: float) -> float:
	var e := absf(u * 2.0 - 1.0)
	return BEAM * 0.5 * (1.0 - pow(e, 3.5) * 0.5)

## How far the gunwale sweeps up toward the ends.
static func _sheer(u: float) -> float:
	var e := absf(u * 2.0 - 1.0)
	return pow(e, 2.6) * DEPTH * 0.32

static func _hull(b: M7MeshBuilder) -> void:
	var n := 10
	for i in n:
		var u0 := float(i) / n
		var u1 := float(i + 1) / n
		var z0 := lerpf(-HALF, HALF, u0)
		var z1 := lerpf(-HALF, HALF, u1)
		var h0 := _hw(u0)
		var h1 := _hw(u1)
		var s0 := _sheer(u0)
		var s1 := _sheer(u1)
		var inside := Vector3(0, DEPTH * 0.5, (z0 + z1) * 0.5)
		for sx in [-1.0, 1.0]:
			# keel, chine, a mid strake and the gunwale: four planked bands, clinker-laid in alternate tones
			var k0 := Vector3(0, 0.004 + s0 * 0.2, z0)
			var k1 := Vector3(0, 0.004 + s1 * 0.2, z1)
			var c0 := Vector3(sx * h0 * 0.55, DEPTH * 0.3 + s0 * 0.5, z0)
			var c1 := Vector3(sx * h1 * 0.55, DEPTH * 0.3 + s1 * 0.5, z1)
			var m0 := Vector3(sx * h0 * 0.9, DEPTH * 0.62 + s0 * 0.8, z0)
			var m1 := Vector3(sx * h1 * 0.9, DEPTH * 0.62 + s1 * 0.8, z1)
			var g0 := Vector3(sx * h0, DEPTH + s0, z0)
			var g1 := Vector3(sx * h1, DEPTH + s1, z1)
			b.quad(k0, k1, c1, c0, PLANK_LOW, inside)
			b.quad(c0, c1, m1, m0, PLANK_MID, inside)
			b.quad(m0, m1, g1, g0, PLANK_UP, inside)
			# a rubbing strake along the gunwale
			b.pole(g0 + Vector3(0, -0.006, 0), g1 + Vector3(0, -0.006, 0), 0.0028, WOOD)
		# the deck between the gunwales
		var d0 := h0 * 0.92
		var d1 := h1 * 0.92
		b.quad(Vector3(-d0, DEPTH * 0.7 + s0 * 0.8, z0), Vector3(d0, DEPTH * 0.7 + s0 * 0.8, z0), Vector3(d1, DEPTH * 0.7 + s1 * 0.8, z1),
			Vector3(-d1, DEPTH * 0.7 + s1 * 0.8, z1), DECK, Vector3(0, -1, 0))
		# the owner's colour painted along the top of the gunwale
		b.tag = CIV
		for sx in [-1.0, 1.0]:
			b.pole(Vector3(sx * h0, DEPTH + s0 + 0.003, z0), Vector3(sx * h1, DEPTH + s1 + 0.003, z1), 0.0045, Color.WHITE)
		b.tag = PLAIN
	# the ends: a flat transom at the stern and a blunt stem at the bow, closing the planking
	for end in [0.0, 1.0]:
		var z: float = lerpf(-HALF, HALF, end)
		var h := _hw(end)
		var s := _sheer(end)
		var hint := Vector3(0, DEPTH * 0.5, 0.0)
		b.quad(Vector3(-h * 0.55, DEPTH * 0.3 + s * 0.5, z), Vector3(h * 0.55, DEPTH * 0.3 + s * 0.5, z), Vector3(h * 0.9, DEPTH * 0.62 + s * 0.8, z),
			Vector3(-h * 0.9, DEPTH * 0.62 + s * 0.8, z), PLANK_MID, hint)
		b.quad(Vector3(-h * 0.9, DEPTH * 0.62 + s * 0.8, z), Vector3(h * 0.9, DEPTH * 0.62 + s * 0.8, z), Vector3(h, DEPTH + s, z), Vector3(-h, DEPTH + s, z),
			PLANK_UP, hint)
	# the stem and stern posts, straight and raked outward
	b.pole(Vector3(0, 0.006, HALF - 0.004), Vector3(0, DEPTH + 0.05, HALF + 0.032), 0.0062, WOOD)
	b.pole(Vector3(0, 0.006, -HALF + 0.004), Vector3(0, DEPTH + 0.05, -HALF - 0.026), 0.0062, WOOD)

## A castle on the deck: a platform with plank walls and a crenellated top, `len` long from z0 toward the end, `wid` wide.
static func _castle(b: M7MeshBuilder, z_mid: float, len: float, wid: float, y: float, rail: float) -> void:
	b.box(Vector3(0, y, z_mid), Vector3(wid, 0.006, len), PLANK_UP, DECK)
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * (wid * 0.5 - 0.003), y + rail * 0.5, z_mid), Vector3(0.006, rail, len), PLANK_MID, PLANK_UP)
		for k in 4:
			if k % 2 == 0:
				b.box(Vector3(sx * (wid * 0.5 - 0.003), y + rail + 0.005, z_mid - len * 0.4 + len * 0.8 * k / 3.0), Vector3(0.007, 0.01, len * 0.18), PLANK_UP)
	# the closed end wall and a deck-level underpinning of posts
	var end_z: float = z_mid + (len * 0.5 if z_mid > 0.0 else -len * 0.5)
	b.box(Vector3(0, y + rail * 0.5, end_z), Vector3(wid, rail, 0.006), PLANK_MID, PLANK_UP)
	var open_z: float = z_mid - (len * 0.5 if z_mid > 0.0 else -len * 0.5)
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(sx * (wid * 0.5 - 0.004), DEPTH * 0.7, open_z), Vector3(sx * (wid * 0.5 - 0.004), y, open_z), 0.0035, WOOD)

static func _barrel(b: M7MeshBuilder, p: Vector3) -> void:
	b.prism(p, 0.0105, 0.0105, 0.02, 8, WOOD_LIGHT, WOOD_LIGHT.lightened(0.1))
	b.prism(p + Vector3(0, 0.005, 0), 0.0113, 0.0113, 0.0022, 8, IRON, IRON)
	b.prism(p + Vector3(0, 0.0135, 0), 0.0113, 0.0113, 0.0022, 8, IRON, IRON)

static func _cog(kit, lateen: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1450 + (1 if lateen else 0))
	b.tag = PLAIN
	_hull(b)
	# the aftercastle (high, over the stern) and a smaller forecastle
	var deck_y := DEPTH * 0.7
	_castle(b, -HALF + 0.075, 0.15, BEAM * 0.76, deck_y + 0.034, 0.03)
	_castle(b, HALF - 0.045, 0.09, BEAM * 0.62, deck_y + 0.02, 0.024)
	# the cargo in the waist: barrels, bales and sacks, more than a galley could stow
	for r in 2:
		for k in 3:
			_barrel(b, Vector3(-0.034 + 0.034 * k, deck_y + 0.004, -0.09 - 0.026 * r))
	for k in 2:
		b.box(Vector3(-0.03 + 0.06 * k, deck_y + 0.014, 0.1), Vector3(0.036, 0.024, 0.03), BALE, BALE.lightened(0.08))
		b.box(Vector3(-0.03 + 0.06 * k, deck_y + 0.016, 0.135), Vector3(0.036, 0.024, 0.026), BALE.darkened(0.08), BALE)
	for k in 3:
		b.blob(Vector3(-0.036 + 0.036 * k, deck_y + 0.012, 0.05 + 0.006 * (k % 2)), Vector3(0.015, 0.012, 0.012), 5, 2, SACK, 0.1, SACK.darkened(0.12))
	# the mast, a crow's nest, the yard and the great square sail (a stripe in the owner's colour between two pale panels)
	var mz := 0.0
	b.pole(Vector3(0, deck_y, mz), Vector3(0, 0.34, mz), 0.0052, WOOD)
	b.prism(Vector3(0, 0.3, mz), 0.014, 0.017, 0.011, 8, WOOD_LIGHT, WOOD)
	b.pole(Vector3(-0.092, 0.27, mz), Vector3(0.092, 0.27, mz), 0.0038, WOOD)
	var cols := 4
	var rws := 4
	var top := 0.268
	var h := 0.15
	var half_w := 0.088
	for r in rws:
		var y0 := top - r * h / rws
		var y1 := y0 - h / rws
		for c in cols:
			var x0 := -half_w + c * 2.0 * half_w / cols
			var x1 := x0 + 2.0 * half_w / cols
			var b0 := M7LooksKit._belly(x0, half_w, y0, top, h) * 1.1
			var b1 := M7LooksKit._belly(x1, half_w, y0, top, h) * 1.1
			var b2 := M7LooksKit._belly(x1, half_w, y1, top, h) * 1.1
			var b3 := M7LooksKit._belly(x0, half_w, y1, top, h) * 1.1
			b.tag = CIV if (c == 1 or c == 2) else PLAIN
			b.quad(Vector3(x0, y0, mz + b0), Vector3(x1, y0, mz + b1), Vector3(x1, y1, mz + b2), Vector3(x0, y1, mz + b3),
				Color.WHITE if (c == 1 or c == 2) else SAIL_CREAM, Vector3(0, 0.17, -0.3))
	b.tag = PLAIN
	# a pennant at the masthead in the owner's colour, stays to the bow and the stern, sheets to the quarters
	b.tag = CIV
	b.quad(Vector3(0, 0.345, mz), Vector3(0.07, 0.335, mz), Vector3(0.07, 0.317, mz), Vector3(0, 0.325, mz), Color.WHITE, Vector3(0, 0.33, 1.0))
	b.quad(Vector3(0, 0.345, mz), Vector3(0.07, 0.335, mz), Vector3(0.07, 0.317, mz), Vector3(0, 0.325, mz), Color.WHITE, Vector3(0, 0.33, -1.0))
	b.tag = PLAIN
	b.pole(Vector3(0, 0.335, mz), Vector3(0, DEPTH + 0.06, HALF + 0.03), 0.0014, ROPE)
	b.pole(Vector3(0, 0.335, mz), Vector3(0, DEPTH + 0.07, -HALF - 0.02), 0.0014, ROPE)
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(sx * 0.09, 0.27, mz), Vector3(sx * 0.05, deck_y + 0.04, 0.12), 0.0013, ROPE)
	if not lateen:
		# a steering oar hung over the starboard quarter: a long loom with a broad blade, and a tiller inboard
		b.pole(Vector3(BEAM * 0.5 + 0.004, DEPTH + 0.05, -HALF + 0.05), Vector3(BEAM * 0.5 + 0.03, -0.012, -HALF + 0.028), 0.0034, WOOD_LIGHT)
		b.box(Vector3(BEAM * 0.5 + 0.032, -0.008, -HALF + 0.026), Vector3(0.006, 0.034, 0.02), WOOD_LIGHT)
		b.pole(Vector3(BEAM * 0.5 + 0.004, DEPTH + 0.05, -HALF + 0.05), Vector3(0.03, DEPTH + 0.065, -HALF + 0.09), 0.0028, WOOD)
	else:
		# the sternpost rudder: a plank hung on the stern post with pintles, a tiller running forward over the aftercastle
		b.box(Vector3(0, 0.03, -HALF - 0.022), Vector3(0.006, 0.07, 0.026), WOOD_LIGHT, PLANK_UP)
		for y in [0.012, 0.045]:
			b.box(Vector3(0, y, -HALF - 0.01), Vector3(0.012, 0.005, 0.014), IRON)
		b.pole(Vector3(0, deck_y + 0.058, -HALF + 0.01), Vector3(0, deck_y + 0.066, -HALF + 0.11), 0.0028, WOOD)
		# the mizzen: a short mast on the aftercastle carrying a long yard slanted across it and a triangular sail of the pale cloth
		var mm := Vector3(0, deck_y + 0.034, -0.165)
		b.pole(mm, mm + Vector3(0, 0.15, 0), 0.0042, WOOD)
		var fore := Vector3(0, mm.y + 0.04, -0.085)
		var aft := Vector3(0, mm.y + 0.18, -0.255)
		var clew := Vector3(0, mm.y + 0.05, -0.248)
		b.pole(fore, aft, 0.0034, WOOD_LIGHT)
		var g := (fore + aft + clew) / 3.0 + Vector3(0.022, 0, 0)
		for sgn in [-1.0, 1.0]:
			var away := g + Vector3(sgn * 0.4, 0, 0)
			b.tri(fore, aft, g, SAIL_CREAM, away)
			b.tri(aft, clew, g, SAIL_CREAM.darkened(0.04), away)
			b.tri(clew, fore, g, SAIL_CREAM.darkened(0.08), away)
		b.pole(clew, Vector3(0.03, deck_y + 0.04, -0.2), 0.0012, ROPE)
	return b.commit()
