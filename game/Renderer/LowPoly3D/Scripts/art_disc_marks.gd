## Discovery (E6) marks: the colony pennant (TER-11) and the institution state glyphs, docs/MVP/39 §7.3 and §8.1.
##
## Not wired to the simulation yet (docs/MVP/39 §9: `colonyState` and `discWorkState` carry which of them to draw, and why); photographed in the Discovery
## gallery (LowPoly3DView.show_discovery_gallery). Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.1 tall). The glyphs are small flat
## plates standing in the XY plane facing +Z, about 0.06 across, set over a building at overview zoom; they carry the reason on hover, not in the picture.
##
##   colony_pennant     a tall pole with a swallow-tailed pennant in the owner's colour (TAG_CIV) bearing a small white ship, set on the Heart of a settlement the
##                      capital cannot reach on foot (a colony)
##   mark_dormant       a grey plate with a pause bar: shuttered, no staff (an empty garden, a society with no scholars, an engine with no engineers)
##   mark_blocked       a red caution triangle with a bar and a dot: working cue but blocked (an engine out of coal, a factory out of power, a fort with no garrison)
##   mark_flooded       a blue water drop on a dark plate: the coal mine's deep seam has flooded and no engine pumps it
##   mark_anchor        a pale anchor on a dark plate: a galleon lies on station at its home harbor, its crew out of the defense pool (docs/MVP/39 §7.1, A-17)
##   mark_strained      a yellow ring: a colony whose Cohesion is low from sea strain
##   colonist_group     the founding of a colony (docs/MVP/39 §7.3): three settlers a tenth tall with bundles, a spade and a crate on the beach, a pennant in the
##                      owner's colour planted in the ground; stands beside a beached caravel for the first seasons of a new colony
##   colony_flag_free   a colony that has broken away: the owner's pennant struck and a plain grey-green flag run up on a short pole, a snapped tether rope on the ground
##   cargo_coal         a load for a cart, barge or quay (the flows layer): a heap of black coal lumps in a low plank crate, a couple of lumps spilled
##   cargo_coke         the same load in pale silver-grey coke, lighter and more porous looking
##   cargo_sugar        a tray of refined sugar loaves: white cones in blue paper wrappers, stacked three over four
##   cargo_cod          salt cod: a bundle of flat split fish pressed together and tied with rope, standing beside a small cask
##   mark_blockaded     a red ring with a small ship stood in it: a colony whose harbor is under blockade
extends RefCounted

const WOOD := Color("#5e4631")
const PLATE := Color("#2a2119")
const GREY := Color("#8a8f98")
const GREY_DARK := Color("#5a5f68")
const RED := Color("#c9342a")
const RED_DARK := Color("#8a2219")
const YELLOW := Color("#e6c230")
const BLUE := Color("#3f82c4")
const BLUE_LIGHT := Color("#8fc4ee")
const WHITE := Color("#f4eee0")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE

const SIZE := 0.03

static func build(kit) -> void:
	kit.meshes["colony_pennant"] = _pennant(kit)
	kit.meshes["mark_dormant"] = _dormant(kit)
	kit.meshes["mark_blocked"] = _blocked(kit)
	kit.meshes["mark_flooded"] = _flooded(kit)
	kit.meshes["mark_anchor"] = _anchor(kit)
	kit.meshes["mark_strained"] = _ring(kit, YELLOW, false, 2201)
	kit.meshes["mark_blockaded"] = _ring(kit, RED, true, 2202)
	kit.meshes["colonist_group"] = _colonists(kit)
	kit.meshes["colony_flag_free"] = _free_flag(kit)
	kit.meshes["cargo_coal"] = _cargo_coal(kit, false)
	kit.meshes["cargo_coke"] = _cargo_coal(kit, true)
	kit.meshes["cargo_sugar"] = _cargo_sugar(kit)
	kit.meshes["cargo_cod"] = _cargo_cod(kit)

## A flat disc plate (a short 12-sided cylinder standing along Z) of radius r at p.
static func _disc(b: M7MeshBuilder, p: Vector3, r: float, col: Color, depth: float = 0.004) -> void:
	for i in 12:
		var a0 := TAU * i / 12.0
		var a1 := TAU * (i + 1) / 12.0
		b.tri(p + Vector3(0, 0, depth), p + Vector3(cos(a0), sin(a0), 0) * r + Vector3(0, 0, depth), p + Vector3(cos(a1), sin(a1), 0) * r + Vector3(0, 0, depth), col, p + Vector3(0, 0, -1.0))
		b.tri(p, p + Vector3(cos(a0), sin(a0), 0) * r, p + Vector3(cos(a1), sin(a1), 0) * r, col.darkened(0.25), p + Vector3(0, 0, 1.0))
		b.quad(p + Vector3(cos(a0), sin(a0), 0) * r, p + Vector3(cos(a1), sin(a1), 0) * r, p + Vector3(cos(a1), sin(a1), depth) * r * Vector3(1, 1, 0) + Vector3(0, 0, depth),
			p + Vector3(cos(a0), sin(a0), 0) * r + Vector3(0, 0, depth), col.darkened(0.15), p)

static func _ring_plate(b: M7MeshBuilder, p: Vector3, r0: float, r1: float, col: Color) -> void:
	for i in 14:
		var a0 := TAU * i / 14.0
		var a1 := TAU * (i + 1) / 14.0
		var o0 := p + Vector3(cos(a0), sin(a0), 0)
		var o1 := p + Vector3(cos(a1), sin(a1), 0)
		b.quad(o0 * 1.0 + (o0 - p) * (r0 - 1.0) + Vector3(0, 0, 0.004), o1 + (o1 - p) * (r0 - 1.0) + Vector3(0, 0, 0.004), o1 + (o1 - p) * (r1 - 1.0) + Vector3(0, 0, 0.004),
			o0 + (o0 - p) * (r1 - 1.0) + Vector3(0, 0, 0.004), col, p + Vector3(0, 0, -1.0))
		b.quad(o0 + (o0 - p) * (r0 - 1.0), o1 + (o1 - p) * (r0 - 1.0), o1 + (o1 - p) * (r1 - 1.0), o0 + (o0 - p) * (r1 - 1.0), col.darkened(0.25), p + Vector3(0, 0, 1.0))

static func _ship(b: M7MeshBuilder, p: Vector3, s: float, col: Color) -> void:
	# a tiny ship silhouette in the XY plane: a hull wedge, a mast and two sails
	b.quad(p + Vector3(-1.0, -0.2, 0) * s, p + Vector3(1.0, -0.2, 0) * s, p + Vector3(0.7, -0.55, 0) * s, p + Vector3(-0.7, -0.55, 0) * s, col, p + Vector3(0, 0, -1.0))
	b.quad(p + Vector3(-1.0, -0.2, 0) * s, p + Vector3(1.0, -0.2, 0) * s, p + Vector3(0.7, -0.55, 0) * s, p + Vector3(-0.7, -0.55, 0) * s, col.darkened(0.2), p + Vector3(0, 0, 1.0))
	b.tri(p + Vector3(-0.05, -0.15, 0) * s, p + Vector3(-0.05, 1.0, 0) * s, p + Vector3(-0.85, -0.15, 0) * s, col, p + Vector3(0, 0, -1.0))
	b.tri(p + Vector3(0.05, -0.15, 0) * s, p + Vector3(0.05, 0.9, 0) * s, p + Vector3(0.8, -0.15, 0) * s, col, p + Vector3(0, 0, -1.0))
	b.tri(p + Vector3(-0.05, -0.15, 0) * s, p + Vector3(-0.05, 1.0, 0) * s, p + Vector3(-0.85, -0.15, 0) * s, col.darkened(0.2), p + Vector3(0, 0, 1.0))
	b.tri(p + Vector3(0.05, -0.15, 0) * s, p + Vector3(0.05, 0.9, 0) * s, p + Vector3(0.8, -0.15, 0) * s, col.darkened(0.2), p + Vector3(0, 0, 1.0))

static func _pennant(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2200)
	b.tag = PLAIN
	b.prism(Vector3.ZERO, 0.007, 0.006, 0.01, 6, GREY_DARK, GREY)
	b.pole(Vector3(0, 0.008, 0), Vector3(0, 0.15, 0), 0.0024, WOOD, 5)
	b.blob(Vector3(0, 0.152, 0), Vector3(0.0045, 0.0045, 0.0045), 4, 2, Color("#c9973c"), 0.0)
	# the swallow-tailed pennant in the owner's colour, flying toward +X from near the head of the pole
	b.tag = CIV
	var top := Vector3(0, 0.145, 0)
	var low := Vector3(0, 0.095, 0)
	var tip := Vector3(0.1, 0.12, 0)
	var notch := Vector3(0.075, 0.12, 0)
	b.tri(top, tip + Vector3(0, 0.025, 0), notch, Color.WHITE, Vector3(0, 0, -1.0))
	b.tri(low, tip - Vector3(0, 0.025, 0), notch, Color.WHITE, Vector3(0, 0, -1.0))
	b.quad(top, notch, low + (notch - low) * 0.0, low, Color.WHITE, Vector3(0, 0, -1.0))
	b.tri(top, tip + Vector3(0, 0.025, 0), notch, Color.WHITE, Vector3(0, 0, 1.0))
	b.tri(low, tip - Vector3(0, 0.025, 0), notch, Color.WHITE, Vector3(0, 0, 1.0))
	b.quad(top, notch, low + (notch - low) * 0.0, low, Color.WHITE, Vector3(0, 0, 1.0))
	# the white ship on the cloth, a little off the face so it does not fight it
	b.tag = PLAIN
	_ship(b, Vector3(0.03, 0.115, 0.0009), 0.016, WHITE)
	return b.commit()

static func _dormant(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2210)
	b.tag = PLAIN
	_disc(b, Vector3.ZERO, SIZE, GREY_DARK)
	b.box(Vector3(-0.009, 0.0, 0.0046), Vector3(0.0085, 0.032, 0.002), GREY)
	b.box(Vector3(0.009, 0.0, 0.0046), Vector3(0.0085, 0.032, 0.002), GREY)
	return b.commit()

static func _blocked(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2211)
	b.tag = PLAIN
	var s := SIZE * 1.15
	var a := Vector3(0, s * 0.95, 0)
	var l := Vector3(-s * 0.95, -s * 0.62, 0)
	var r := Vector3(s * 0.95, -s * 0.62, 0)
	b.tri(a, l, r, RED, Vector3(0, 0, -1.0))
	b.tri(a + Vector3(0, 0, 0.004), l + Vector3(0, 0, 0.004), r + Vector3(0, 0, 0.004), RED_DARK, Vector3(0, 0, 1.0))
	b.box(Vector3(0, 0.003, 0.0035), Vector3(0.0075, 0.021, 0.002), WHITE)
	b.blob(Vector3(0, -0.0165, 0.0035), Vector3(0.0048, 0.0048, 0.002), 5, 2, WHITE, 0.0)
	return b.commit()

static func _flooded(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2212)
	b.tag = PLAIN
	_disc(b, Vector3.ZERO, SIZE, PLATE)
	# a water drop: a point above a round belly
	b.blob(Vector3(0, -0.005, 0.0046), Vector3(0.0125, 0.0125, 0.002), 7, 3, BLUE, 0.0, BLUE)
	b.tri(Vector3(0, 0.02, 0.0046), Vector3(-0.0115, 0.0, 0.0046), Vector3(0.0115, 0.0, 0.0046), BLUE, Vector3(0, 0, -1.0))
	b.blob(Vector3(-0.0045, -0.0025, 0.0062), Vector3(0.0036, 0.0045, 0.001), 4, 2, BLUE_LIGHT, 0.0)
	return b.commit()

static func _anchor(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2213)
	b.tag = PLAIN
	_disc(b, Vector3.ZERO, SIZE, PLATE)
	var z := 0.0046
	# the ring at the head, the shank, the stock across it, and two curved arms with a fluke at each tip
	_ring_plate(b, Vector3(0, 0.0185, z - 0.0004), 0.0032, 0.0052, WHITE)
	b.box(Vector3(0, -0.0015, z), Vector3(0.0042, 0.0285, 0.002), WHITE)
	b.box(Vector3(0, 0.0105, z), Vector3(0.0175, 0.0036, 0.002), WHITE)
	for sx in [-1.0, 1.0]:
		var base := Vector3(0, -0.0145, z)
		var tip := Vector3(0.0135 * sx, -0.0055, z)
		b.quad(base + Vector3(0, -0.002, 0), tip + Vector3(0, -0.002, 0), tip + Vector3(0, 0.0018, 0), base + Vector3(0, 0.002, 0), WHITE, Vector3(0, 0, -1.0))
		b.tri(tip + Vector3(0.0, 0.0105, 0), tip + Vector3(0.0065 * sx, -0.0012, 0), tip + Vector3(-0.0035 * sx, -0.0012, 0), WHITE, Vector3(0, 0, -1.0))
	return b.commit()

static func _ring(kit, col: Color, ship: bool, seed: int) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(seed)
	b.tag = PLAIN
	_ring_plate(b, Vector3.ZERO, SIZE * 0.78, SIZE * 1.0, col)
	if ship:
		_ship(b, Vector3(0, 0.003, 0.002), 0.012, col)
	return b.commit()

# ------------------------------------------------------------------ cargo props

static func _cargo_coal(kit, coke: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2230 if not coke else 2231)
	b.tag = PLAIN
	var plank := Color("#8a6a48")
	b.box(Vector3(0, 0.008, 0), Vector3(0.07, 0.016, 0.05), plank, plank.lightened(0.06))
	b.box(Vector3(0, 0.0075, 0.0255), Vector3(0.072, 0.01, 0.002), plank.darkened(0.2))
	var col := Color("#26262b") if not coke else Color("#8c8f96")
	var light := Color("#3d3d44") if not coke else Color("#adb0b6")
	b.blob(Vector3(0, 0.02, 0), Vector3(0.033, 0.015, 0.023), 7, 3, col, 0.2, col.darkened(0.2))
	for k in 7:
		var a := TAU * k / 7.0
		b.blob(Vector3(cos(a) * 0.017, 0.029 + 0.002 * (k % 3), sin(a) * 0.011), Vector3(0.007, 0.006, 0.007), 4, 2, light if k % 2 == 0 else col, 0.25, col.darkened(0.2))
	b.blob(Vector3(0.039, 0.005, 0.01), Vector3(0.006, 0.005, 0.006), 4, 2, col, 0.2)
	b.blob(Vector3(-0.037, 0.005, -0.012), Vector3(0.005, 0.0045, 0.005), 4, 2, light, 0.2)
	return b.commit()

static func _cargo_sugar(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2232)
	b.tag = PLAIN
	b.box(Vector3(0, 0.005, 0), Vector3(0.07, 0.01, 0.05), Color("#8a6a48"), Color("#a07a54"))
	# loaves: cones with a rounded tip, wrapped in blue paper below and bare white above, four in the back row, three in front
	for row in 2:
		var n := 4 if row == 0 else 3
		for k in n:
			var x := -0.024 + 0.016 * k + (0.008 if row == 1 else 0.0)
			var z := -0.011 if row == 0 else 0.011
			b.prism(Vector3(x, 0.01, z), 0.0068, 0.004, 0.012, 7, Color("#3f6fa6"), Color("#3f6fa6"))
			b.prism(Vector3(x, 0.022, z), 0.004, 0.0, 0.012, 7, Color("#f4f0e6"), Color("#f4f0e6"))
			b.blob(Vector3(x, 0.034, z), Vector3(0.0018, 0.0018, 0.0018), 4, 2, Color("#f4f0e6"), 0.0)
	return b.commit()

static func _cargo_cod(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2233)
	b.tag = PLAIN
	var fish := Color("#e0d6b4")
	var fish_dark := Color("#bdb08a")
	# a stack of split fish lying flat on a board, each a clear fish shape from above (a lozenge body with a forked tail and a dark eye), turned a little
	# against the one below so the stack reads as fish, not a mat
	b.box(Vector3(0, 0.003, 0), Vector3(0.07, 0.006, 0.042), Color("#8a6a48"), Color("#a07a54"))
	for k in 5:
		var y := 0.0075 + 0.0042 * k
		var yaw := (float(k) - 2.0) * 0.16
		var c := fish if k % 2 == 0 else fish_dark
		var d := Vector3(cos(yaw), 0, sin(yaw))
		var sd := Vector3(-d.z, 0, d.x)
		var o := Vector3(-0.004, y, 0.0)
		var nose := o - d * 0.027
		var tail := o + d * 0.02
		b.quad(nose, o + sd * 0.01, tail, o - sd * 0.01, c, Vector3(0, -1.0, 0))
		b.tri(tail, tail + d * 0.012 + sd * 0.0085, tail + d * 0.006, c.darkened(0.08), Vector3(0, -1.0, 0))
		b.tri(tail, tail + d * 0.012 - sd * 0.0085, tail + d * 0.006, c.darkened(0.08), Vector3(0, -1.0, 0))
		b.blob(nose + d * 0.006 + Vector3(0, 0.0015, 0) + sd * 0.0025, Vector3(0.0018, 0.0012, 0.0018), 4, 2, Color("#2a2119"), 0.0)
	# a rope round the stack, and a small cask of salt at the end
	b.pole(Vector3(-0.012, 0.0285, -0.016), Vector3(-0.012, 0.0285, 0.016), 0.0014, Color("#c8b88a"), 3)
	var wood := Color("#8a6a48")
	b.prism(Vector3(0.05, 0.0, 0.0), 0.0092, 0.0092, 0.019, 8, wood, wood.lightened(0.1))
	b.prism(Vector3(0.05, 0.004, 0.0), 0.01, 0.01, 0.0018, 8, Color("#4a4f57"), Color("#4a4f57"))
	b.prism(Vector3(0.05, 0.0125, 0.0), 0.01, 0.01, 0.0018, 8, Color("#4a4f57"), Color("#4a4f57"))
	return b.commit()

# ------------------------------------------------------------------ colonists and the free flag

const SKIN := Color("#b07a52")
const CLOTH := Color("#8a6a48")
const BUNDLE := Color("#cdbf9f")
const WOODC := Color("#5e4631")
const FREE_FLAG := Color("#7c8a74")
const FREE_FLAG_DARK := Color("#5c6a56")
const ROPEC := Color("#c8b88a")

static func _settler(b: M7MeshBuilder, p: Vector3, hat: bool) -> void:
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis.IDENTITY, p)
	b.tag = PLAIN
	b.prism(Vector3(-0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, Color("#4a3a2a"), Color("#4a3a2a"))
	b.prism(Vector3(0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, Color("#4a3a2a"), Color("#4a3a2a"))
	b.prism(Vector3(0, 0.034, 0), 0.0185, 0.0155, 0.038, 7, CLOTH, CLOTH)
	b.tag = CIV
	b.prism(Vector3(0, 0.054, 0), 0.0205, 0.009, 0.014, 7, Color.WHITE, Color.WHITE)
	b.tag = PLAIN
	b.blob(Vector3(0, 0.084, 0), Vector3(0.0125, 0.013, 0.0125), 7, 4, SKIN, 0.0)
	if hat:
		b.prism(Vector3(0, 0.091, 0), 0.0185, 0.0185, 0.002, 8, Color("#3a342e"), Color("#3a342e"))
		b.prism(Vector3(0, 0.093, 0), 0.0112, 0.0098, 0.012, 8, Color("#3a342e"), Color("#2a2522"))
	else:
		b.blob(Vector3(0, 0.0905, -0.002), Vector3(0.0128, 0.009, 0.012), 7, 3, Color("#2b201a"), 0.0)
	b.pole(Vector3(-0.02, 0.07, 0), Vector3(-0.024, 0.05, 0.006), 0.0045, CLOTH)
	b.pole(Vector3(0.02, 0.07, 0), Vector3(0.026, 0.05, 0.01), 0.0045, CLOTH)
	b.xf = save

static func _colonists(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2240)
	b.tag = PLAIN
	_settler(b, Vector3(-0.045, 0.0, 0.01), true)
	_settler(b, Vector3(0.0, 0.0, -0.01), false)
	_settler(b, Vector3(0.05, 0.0, 0.015), true)
	# a spade in the first settler's hand, a bundle on the second's back, a bale and a crate at their feet
	b.pole(Vector3(-0.02, 0.0, 0.03), Vector3(-0.016, 0.1, 0.034), 0.0022, WOODC, 4)
	b.box(Vector3(-0.0175, 0.003, 0.03), Vector3(0.009, 0.007, 0.002), Color("#8d949c"))
	b.blob(Vector3(0.0, 0.056, -0.0235), Vector3(0.0125, 0.0155, 0.009), 6, 3, BUNDLE, 0.08, BUNDLE.darkened(0.12))
	b.blob(Vector3(0.026, 0.01, 0.045), Vector3(0.0125, 0.01, 0.011), 5, 2, BUNDLE, 0.08, BUNDLE.darkened(0.12))
	b.box(Vector3(-0.07, 0.009, -0.015), Vector3(0.016, 0.018, 0.016), Color("#8a6a48"), Color("#a07a54"))
	b.box(Vector3(-0.07, 0.009, -0.015), Vector3(0.0172, 0.003, 0.0172), Color("#4a3726"))
	# the owner's pennant planted in the ground: a short pole with a swallow-tailed pennant flying toward +X (as colony_pennant, smaller)
	b.pole(Vector3(0.085, 0.0, -0.03), Vector3(0.085, 0.13, -0.03), 0.0022, WOODC, 5)
	b.tag = CIV
	var top := Vector3(0.085, 0.125, -0.03)
	var low := Vector3(0.085, 0.092, -0.03)
	var tip := Vector3(0.155, 0.11, -0.03)
	var notch := Vector3(0.135, 0.11, -0.03)
	for hint in [-1.0, 1.0]:
		var h: float = hint
		b.tri(top, tip + Vector3(0, 0.015, 0), notch, Color.WHITE, Vector3(0, 0, h))
		b.tri(low, tip - Vector3(0, 0.015, 0), notch, Color.WHITE, Vector3(0, 0, h))
	b.tag = PLAIN
	return b.commit()

static func _free_flag(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2241)
	b.tag = PLAIN
	b.prism(Vector3.ZERO, 0.007, 0.006, 0.01, 6, GREY_DARK, GREY)
	b.pole(Vector3(0, 0.008, 0), Vector3(0, 0.12, 0), 0.0024, WOODC, 5)
	# a plain grey-green flag with a ragged fly: no device, no owner's colour (the colony answers to nobody)
	var top := Vector3(0, 0.116, 0)
	var low := Vector3(0, 0.082, 0)
	var f0 := Vector3(0.05, 0.112, 0)
	var f1 := Vector3(0.056, 0.098, 0)
	var f2 := Vector3(0.046, 0.086, 0)
	for hint in [-1.0, 1.0]:
		var h: float = hint
		var col: Color = FREE_FLAG if h < 0.0 else FREE_FLAG_DARK
		b.quad(top, f0, f1, Vector3(0.0, 0.1, 0), col, Vector3(0, 0, h))
		b.quad(Vector3(0.0, 0.1, 0), f1, f2, low, col.darkened(0.06), Vector3(0, 0, h))
	# the severed tether: a length of rope fallen on the ground, its end frayed
	b.pole(Vector3(0.012, 0.002, 0.02), Vector3(0.05, 0.002, 0.05), 0.0016, ROPEC, 3)
	b.pole(Vector3(0.05, 0.002, 0.05), Vector3(0.07, 0.003, 0.042), 0.0016, ROPEC, 3)
	b.pole(Vector3(0.07, 0.003, 0.042), Vector3(0.078, 0.004, 0.05), 0.0011, ROPEC.darkened(0.15), 3)
	return b.commit()
