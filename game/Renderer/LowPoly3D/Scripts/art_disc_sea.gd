## Discovery (E6) ships: the caravel (SEA-12 Caravels & Ocean Voyages) and the galleon (SEA-13 Galleons & Ships of the Line), docs/MVP/39 §7.1.
##
## Not wired to the simulation yet (docs/MVP/39 §9: a `LOOK_CARAVEL` / `LOOK_GALLEON` bit per civilization chooses the mesh, as for the galley and the cog);
## photographed in the Discovery gallery (LowPoly3DView.show_discovery_gallery). Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.1
## tall). Ships lie along Z with the bow toward +Z, like the galley and the cog; TAG_CIV faces take the owner's colour (the gunwale and gun-port stripe, a
## stripe across every square sail, the masthead pennants).
##
##   caravel   a slim, fast ocean ship: a low forecastle, a small raised poop, three masts: a square sail on the fore and main and a lateen on the mizzen,
##             a short bowsprit, masthead pennants, a few barrels on deck. 0.46 long, against the cog's 0.5 (a caravel is narrower and lower).
##   galleon   a tall armed ship: a long hull with two rows of gun ports on each side, a beakhead and a bowsprit with a spritsail, a high stern castle with
##             a gallery of windows and a lantern, a forecastle, three masts: fore with a course and a topsail, main with a course, topsail and topgallant,
##             and a lateen mizzen. 0.62 long.
extends RefCounted

const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const PLANK_LOW := Color("#6e4c2c")
const PLANK_MID := Color("#8e6838")
const PLANK_UP := Color("#b08250")
const DECK := Color("#c9a56a")
const PITCH := Color("#2c2118")
const CANVAS := Color("#f0e8d4")
const ROPE := Color("#b9a68a")
const IRON := Color("#4a4f57")
const BRASS := Color("#c9973c")
const LANTERN := Color("#ffd27a")
const GLASS := Color("#26323d")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE

static func build(kit) -> void:
	kit.meshes["caravel"] = _caravel(kit)
	kit.meshes["galleon"] = _galleon(kit)

# ------------------------------------------------------------------ the hull

## Half-breadth at u (0 at the stern, 1 at the bow): a flat transom, full amidships a little aft, a sharp cut-water.
static func _hw(u: float, beam: float, bow: float) -> float:
	var full := beam * 0.5
	if u < 0.42:
		return lerpf(full * 0.62, full, sin(u / 0.42 * PI * 0.5))
	var t := (u - 0.42) / 0.58
	return full * (1.0 - pow(t, bow))

static func _sheer(u: float, depth: float, up: float) -> float:
	var e := absf(u * 2.0 - 1.0)
	return pow(e, 2.4) * depth * up

## Plank the hull in three bands each side, close the stern and lay the deck. `ports` is the number of gun ports on each side (0 for none).
static func _hull(b: M7MeshBuilder, length: float, beam: float, depth: float, bow: float, rise: float, ports: int) -> void:
	var half := length * 0.5
	var n := 14
	for i in n:
		var u0 := float(i) / n
		var u1 := float(i + 1) / n
		var z0 := lerpf(-half, half, u0)
		var z1 := lerpf(-half, half, u1)
		var h0 := _hw(u0, beam, bow)
		var h1 := _hw(u1, beam, bow)
		var s0 := _sheer(u0, depth, rise)
		var s1 := _sheer(u1, depth, rise)
		var inside := Vector3(0, depth * 0.5, (z0 + z1) * 0.5)
		for sx in [-1.0, 1.0]:
			var x: float = sx
			var k0 := Vector3(0, 0.004 + s0 * 0.2, z0)
			var k1 := Vector3(0, 0.004 + s1 * 0.2, z1)
			var c0 := Vector3(x * h0 * 0.52, depth * 0.3 + s0 * 0.4, z0)
			var c1 := Vector3(x * h1 * 0.52, depth * 0.3 + s1 * 0.4, z1)
			var m0 := Vector3(x * h0 * 0.92, depth * 0.62 + s0 * 0.8, z0)
			var m1 := Vector3(x * h1 * 0.92, depth * 0.62 + s1 * 0.8, z1)
			var g0 := Vector3(x * h0, depth + s0, z0)
			var g1 := Vector3(x * h1, depth + s1, z1)
			b.quad(k0, k1, c1, c0, PLANK_LOW, inside)
			b.quad(c0, c1, m1, m0, PLANK_MID, inside)
			b.quad(m0, m1, g1, g0, PLANK_UP, inside)
			b.pole(g0 + Vector3(0, -0.005, 0), g1 + Vector3(0, -0.005, 0), 0.0026, WOOD, 4)
		var d0 := h0 * 0.92
		var d1 := h1 * 0.92
		b.quad(Vector3(-d0, depth * 0.7 + s0 * 0.8, z0), Vector3(d0, depth * 0.7 + s0 * 0.8, z0), Vector3(d1, depth * 0.7 + s1 * 0.8, z1),
			Vector3(-d1, depth * 0.7 + s1 * 0.8, z1), DECK, Vector3(0, -1, 0))
		# the owner's colour along the top of the gunwale
		b.tag = CIV
		for sx in [-1.0, 1.0]:
			var x2: float = sx
			b.pole(Vector3(x2 * h0, depth + s0 + 0.003, z0), Vector3(x2 * h1, depth + s1 + 0.003, z1), 0.0045, Color.WHITE)
		b.tag = PLAIN
	# the flat transom closing the stern, planked and with a stripe
	var hs := _hw(0.0, beam, bow)
	var ss := _sheer(0.0, depth, rise)
	var hint := Vector3(0, depth * 0.5, 0.0)
	b.quad(Vector3(-hs * 0.52, depth * 0.3 + ss * 0.4, -half), Vector3(hs * 0.52, depth * 0.3 + ss * 0.4, -half), Vector3(hs * 0.92, depth * 0.62 + ss * 0.8, -half),
		Vector3(-hs * 0.92, depth * 0.62 + ss * 0.8, -half), PLANK_MID, hint)
	b.quad(Vector3(-hs * 0.92, depth * 0.62 + ss * 0.8, -half), Vector3(hs * 0.92, depth * 0.62 + ss * 0.8, -half), Vector3(hs, depth + ss, -half),
		Vector3(-hs, depth + ss, -half), PLANK_UP, hint)
	# the stem: a raked cut-water at the bow
	b.pole(Vector3(0, 0.004, half - 0.002), Vector3(0, depth + _sheer(1.0, depth, rise) + 0.012, half + 0.018), 0.0058, WOOD, 4)
	# gun ports: dark squares on the upper band, each with a lid propped open, a row per side; a second row below on a ship of the line
	if ports > 0:
		for sx in [-1.0, 1.0]:
			var x3: float = sx
			for row in [0, 1]:
				var r: int = row
				if r == 1 and ports < 4:
					continue
				for k in ports:
					var u := 0.26 + 0.5 * (float(k) + 0.5 * float(r)) / ports
					if u > 0.84:
						continue
					var z := lerpf(-half, half, u)
					var hw := _hw(u, beam, bow)
					var y := depth * (0.72 if r == 0 else 0.46) + _sheer(u, depth, rise) * 0.7
					var xo := x3 * (hw * (0.97 if r == 0 else 0.76) + 0.001)
					b.box(Vector3(xo, y, z), Vector3(0.004, 0.011, 0.014), PITCH)
					b.box(Vector3(xo + x3 * 0.0035, y + 0.0095, z), Vector3(0.002, 0.003, 0.016), PLANK_LOW)

## A deckhouse: a raised platform box with plank walls (a castle), `len` long along Z from z0, `wid` wide, `rise` tall above the deck level y.
static func _castle(b: M7MeshBuilder, z0: float, len: float, wid: float, y: float, rise: float, windows: bool) -> void:
	var zc := z0 + len * 0.5
	b.box(Vector3(0, y + rise * 0.5, zc), Vector3(wid, rise, len), PLANK_MID, PLANK_UP)
	b.box(Vector3(0, y + rise + 0.003, zc), Vector3(wid + 0.008, 0.006, len + 0.008), PLANK_UP, DECK)
	b.tag = CIV
	b.box(Vector3(0, y + rise - 0.004, zc), Vector3(wid + 0.0085, 0.008, len + 0.0085), Color.WHITE)
	b.tag = PLAIN
	if windows:
		var back := z0 if z0 < 0.0 else z0 + len
		for k in 5:
			var x := (float(k) - 2.0) * wid * 0.19
			b.box(Vector3(x, y + rise * 0.55, back + (-0.0015 if z0 < 0.0 else 0.0015)), Vector3(wid * 0.1, rise * 0.34, 0.003), GLASS)
		b.tag = EMISSIVE
		b.box(Vector3(0, y + rise * 0.55, back + (-0.0035 if z0 < 0.0 else 0.0035)), Vector3(wid * 0.04, rise * 0.2, 0.0015), LANTERN)
		b.tag = PLAIN

# ------------------------------------------------------------------ rigging

static func _mast(b: M7MeshBuilder, p: Vector3, h: float, r: float) -> void:
	b.pole(p, p + Vector3(0, h, 0), r, WOOD, 6)
	b.blob(p + Vector3(0, h * 0.62, 0), Vector3(r * 3.0, r * 1.2, r * 3.0), 5, 2, WOOD_LIGHT, 0.0)

## A square sail across the ship at p (the mast's foot, on the centreline) from y0 to y1, half-width hw, billowed toward the bow by `bulge`, on a yard at its
## head; a stripe in the owner's colour across it. Two faces (the back in a shadow tone).
static func _square(b: M7MeshBuilder, p: Vector3, y0: float, y1: float, hw: float, bulge: float, stripe: bool = true) -> void:
	var cols := 4
	var save := b.tag
	for face in [0, 1]:
		var front: bool = face == 0
		for i in cols:
			var xa := lerpf(-hw, hw, float(i) / cols)
			var xb := lerpf(-hw, hw, float(i + 1) / cols)
			var za := bulge * sin(PI * float(i) / cols)
			var zb := bulge * sin(PI * float(i + 1) / cols)
			var col: Color = CANVAS if front else CANVAS.darkened(0.14)
			var o: float = 0.0 if front else -0.0008
			b.tag = PLAIN
			b.quad(p + Vector3(xa, y0, za * 0.55 + o), p + Vector3(xb, y0, zb * 0.55 + o), p + Vector3(xb, y1, zb * 0.12 + o), p + Vector3(xa, y1, za * 0.12 + o),
				col, p + Vector3(0, (y0 + y1) * 0.5, 1.0 if front else -1.0))
			if stripe:
				b.tag = CIV
				var ya := lerpf(y0, y1, 0.42)
				var yb := lerpf(y0, y1, 0.56)
				var ka := lerpf(za * 0.55, za * 0.12, 0.42)
				var kb := lerpf(za * 0.55, za * 0.12, 0.56)
				var ka2 := lerpf(zb * 0.55, zb * 0.12, 0.42)
				var kb2 := lerpf(zb * 0.55, zb * 0.12, 0.56)
				var oo: float = (0.0011 if front else -0.0019)
				b.quad(p + Vector3(xa, ya, ka + oo), p + Vector3(xb, ya, ka2 + oo), p + Vector3(xb, yb, kb2 + oo), p + Vector3(xa, yb, kb + oo), Color.WHITE,
					p + Vector3(0, (ya + yb) * 0.5, 1.0 if front else -1.0))
	b.tag = PLAIN
	# the yard at the head and a boom at the foot
	b.pole(p + Vector3(-hw - 0.006, y1, 0.0), p + Vector3(hw + 0.006, y1, 0.0), 0.0032, WOOD_LIGHT, 4)
	b.pole(p + Vector3(-hw * 0.92, y0, bulge * 0.55), p + Vector3(hw * 0.92, y0, bulge * 0.55), 0.0018, ROPE, 3)
	b.tag = save

## A lateen sail on a long slanting yard in the YZ plane over the centreline at p: the yard runs from low at the bow end (z+) up to the masthead, the sail is
## the triangle under it, both faces.
static func _lateen(b: M7MeshBuilder, p: Vector3, yard_lo: Vector3, yard_hi: Vector3, tail: float) -> void:
	var lo := p + yard_lo
	var hi := p + yard_hi
	var tack := p + Vector3(0, yard_lo.y - 0.012, yard_hi.z - tail)
	b.pole(lo, hi, 0.0034, WOOD_LIGHT, 4)
	b.tri(lo, hi, tack, CANVAS, lo + Vector3(1.0, 0.0, 0.0))
	b.tri(lo, hi, tack, CANVAS.darkened(0.14), lo + Vector3(-1.0, 0.0, 0.0))
	b.tag = CIV
	var m0 := lo.lerp(tack, 0.35)
	var m1 := hi.lerp(tack, 0.35)
	var m2 := lo.lerp(tack, 0.5)
	var m3 := hi.lerp(tack, 0.5)
	b.quad(m0 + Vector3(0.0009, 0, 0), m1 + Vector3(0.0009, 0, 0), m3 + Vector3(0.0009, 0, 0), m2 + Vector3(0.0009, 0, 0), Color.WHITE, m0 + Vector3(1, 0, 0))
	b.quad(m0 - Vector3(0.0009, 0, 0), m1 - Vector3(0.0009, 0, 0), m3 - Vector3(0.0009, 0, 0), m2 - Vector3(0.0009, 0, 0), Color.WHITE, m0 - Vector3(1, 0, 0))
	b.tag = PLAIN

static func _pennant(b: M7MeshBuilder, p: Vector3, len: float) -> void:
	b.pole(p, p + Vector3(0, 0.014, 0), 0.0014, WOOD, 3)
	b.tag = CIV
	var q := p + Vector3(0, 0.014, 0)
	b.tri(q, q + Vector3(0, -0.009, -len), q + Vector3(0, 0.0, -len * 0.4), Color.WHITE, q + Vector3(1, 0, 0))
	b.tri(q, q + Vector3(0, -0.009, -len), q + Vector3(0, 0.0, -len * 0.4), Color.WHITE, q + Vector3(-1, 0, 0))
	b.tag = PLAIN

static func _barrel(b: M7MeshBuilder, p: Vector3) -> void:
	b.prism(p, 0.009, 0.009, 0.017, 8, WOOD_LIGHT, WOOD_LIGHT.lightened(0.1))
	b.prism(p + Vector3(0, 0.004, 0), 0.0097, 0.0097, 0.002, 8, IRON, IRON)
	b.prism(p + Vector3(0, 0.0115, 0), 0.0097, 0.0097, 0.002, 8, IRON, IRON)

# ------------------------------------------------------------------ the caravel

static func _caravel(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1900)
	b.tag = PLAIN
	var length := 0.46
	var beam := 0.11
	var depth := 0.055
	_hull(b, length, beam, depth, 1.7, 0.34, 0)
	var half := length * 0.5
	# a low forecastle and a small raised poop with a stern window
	_castle(b, half - 0.075, 0.07, beam * 0.7, depth * 0.72, 0.026, false)
	_castle(b, -half + 0.01, 0.1, beam * 0.74, depth * 0.72, 0.04, true)
	# the bowsprit: a short pole raked up from the stem, with its stay
	b.pole(Vector3(0, depth * 0.9, half - 0.01), Vector3(0, depth * 1.35, half + 0.08), 0.0042, WOOD, 4)
	b.pole(Vector3(0, depth * 1.35, half + 0.08), Vector3(0, 0.15, 0.09), 0.0011, ROPE, 3)
	# three masts: fore (square), main (square, bigger), mizzen (lateen)
	var base := depth * 0.72
	var fore := Vector3(0, base, 0.1)
	var main := Vector3(0, base, -0.01)
	var miz := Vector3(0, base, -0.125)
	_mast(b, fore, 0.2, 0.0034)
	_mast(b, main, 0.27, 0.0042)
	_mast(b, miz, 0.2, 0.0032)
	_square(b, fore, 0.065, 0.185, 0.04, 0.014)
	_square(b, main, 0.07, 0.25, 0.058, 0.018)
	_lateen(b, miz, Vector3(0, 0.05, 0.092), Vector3(0, 0.19, -0.09), 0.02)
	# shrouds and stays: thin lines from each masthead to the rail and to the next mast
	for m in [fore, main]:
		var mp: Vector3 = m
		var top := mp.y + (0.2 if mp == fore else 0.27) * 0.9
		for sx in [-1.0, 1.0]:
			var x: float = sx
			b.pole(Vector3(0, top, mp.z), Vector3(x * beam * 0.46, depth, mp.z - 0.012), 0.0009, ROPE, 3)
			b.pole(Vector3(0, top, mp.z), Vector3(x * beam * 0.46, depth, mp.z + 0.012), 0.0009, ROPE, 3)
	b.pole(Vector3(0, base + 0.265, -0.01), Vector3(0, base + 0.195, 0.1), 0.0009, ROPE, 3)
	# masthead pennants
	_pennant(b, Vector3(0, base + 0.27, -0.01), 0.04)
	_pennant(b, Vector3(0, base + 0.2, 0.1), 0.03)
	_pennant(b, Vector3(0, base + 0.2, -0.125), 0.03)
	# a few barrels and a hatch amidships
	for k in 3:
		_barrel(b, Vector3(-0.026 + 0.026 * k, depth * 0.72, 0.045))
	b.box(Vector3(0, depth * 0.72 + 0.004, -0.055), Vector3(0.04, 0.007, 0.028), PLANK_LOW, PLANK_MID)
	return b.commit()

# ------------------------------------------------------------------ the galleon

static func _galleon(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1910)
	b.tag = PLAIN
	var length := 0.62
	var beam := 0.16
	var depth := 0.085
	_hull(b, length, beam, depth, 1.45, 0.32, 5)
	var half := length * 0.5
	var base := depth * 0.72
	# the stern castle: two tiers with a gallery of windows and a lantern on top; a forecastle at the bow with a beakhead
	_castle(b, -half + 0.01, 0.17, beam * 0.86, base, 0.05, true)
	_castle(b, -half + 0.02, 0.1, beam * 0.74, base + 0.05, 0.04, true)
	_castle(b, half - 0.1, 0.09, beam * 0.74, base, 0.034, false)
	b.pole(Vector3(0, base + 0.09, -half + 0.03), Vector3(0, base + 0.115, -half + 0.03), 0.0018, WOOD, 4)
	b.tag = EMISSIVE
	b.box(Vector3(0, base + 0.123, -half + 0.03), Vector3(0.011, 0.014, 0.011), LANTERN)
	b.tag = PLAIN
	# the beakhead and bowsprit: a long raked spar from the forecastle, with its spritsail on a small yard
	b.pole(Vector3(0, base + 0.02, half - 0.02), Vector3(0, base + 0.07, half + 0.11), 0.005, WOOD, 4)
	b.box(Vector3(0, base + 0.014, half + 0.012), Vector3(beam * 0.2, 0.007, 0.05), PLANK_UP, DECK)
	_square(b, Vector3(0, base + 0.058, half + 0.062), 0.0, 0.04, 0.032, 0.008)
	b.pole(Vector3(0, base + 0.07, half + 0.11), Vector3(0, 0.26, 0.14), 0.0011, ROPE, 3)
	# three masts: fore (course and topsail), main (course, topsail, topgallant), mizzen (lateen and a small topsail)
	var fore := Vector3(0, base, 0.13)
	var main := Vector3(0, base, 0.01)
	var miz := Vector3(0, base, -0.15)
	_mast(b, fore, 0.3, 0.0042)
	_mast(b, main, 0.4, 0.0052)
	_mast(b, miz, 0.28, 0.0036)
	_square(b, fore, 0.052, 0.14, 0.058, 0.016)
	_square(b, fore, 0.15, 0.235, 0.044, 0.012)
	_square(b, main, 0.056, 0.17, 0.075, 0.02)
	_square(b, main, 0.18, 0.285, 0.058, 0.015)
	_square(b, main, 0.295, 0.37, 0.04, 0.01, false)
	_lateen(b, miz, Vector3(0, 0.058, 0.115), Vector3(0, 0.215, -0.11), 0.03)
	_square(b, miz, 0.225, 0.27, 0.026, 0.006, false)
	# shrouds and stays from each masthead to the rails
	for spec in [[fore, 0.3], [main, 0.4], [miz, 0.28]]:
		var mp: Vector3 = spec[0]
		var top: float = mp.y + float(spec[1]) * 0.6
		for sx in [-1.0, 1.0]:
			var x: float = sx
			b.pole(Vector3(0, top, mp.z), Vector3(x * beam * 0.46, depth, mp.z - 0.016), 0.001, ROPE, 3)
			b.pole(Vector3(0, top, mp.z), Vector3(x * beam * 0.46, depth, mp.z + 0.016), 0.001, ROPE, 3)
	# pennants and the great flag at the stern
	_pennant(b, Vector3(0, base + 0.4, 0.01), 0.05)
	_pennant(b, Vector3(0, base + 0.3, 0.13), 0.04)
	_pennant(b, Vector3(0, base + 0.285, -0.15), 0.04)
	# the deck: barrels, a hatch and two guns on the waist
	for k in 2:
		_barrel(b, Vector3(-0.03 + 0.06 * k, base, 0.065))
	b.box(Vector3(0, base + 0.004, -0.07), Vector3(0.05, 0.008, 0.034), PLANK_LOW, PLANK_MID)
	return b.commit()
