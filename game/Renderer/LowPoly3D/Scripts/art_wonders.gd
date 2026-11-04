## The Iron Age's three Wonders: the Great Dome (BLD-11), the Great Library (LRN-06) and the Great Lighthouse (SEA-08).
##
## Laid out by world_builder.gd (`_place_civic` for the first two, `_lighthouse` on the shore); the lighthouse's fire is the war rig's. Same units as the rest
## of the kit (1.0 = one tile column, an adult is ~0.1 tall). Buildings face +Z (their front, toward the hearth); TAG_CIV faces take the owner's colour.
##
##   great_dome         a columned portico on a stepped podium before a round drum under a great dome with an oculus; the owner's banner
##   great_library      a long colonnaded hall under a pedimented roof, a wing at each end, the owner's frieze and a scroll medallion on the pediment
##   great_lighthouse   a tall tower in three stages (square, eight-sided, round) on a stone platform, a gallery and a lantern room where the fire burns
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const LIME := Color("#e2d6bc")
const LIME_TOP := Color("#f2ead8")
const CONCRETE := Color("#cfc7b4")
const ROOF := Color("#a8492c")
const ROOF_DARK := Color("#7f3622")
const ARCH := Color("#2a2119")
const WOOD := Color("#5e4631")
const GOLD := Color("#c9973c")
const GLASS := Color("#fff2c0")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE

## The height of the lantern's middle above the mesh origin, for the fire (the builder scales it).
const LANTERN_Y := 0.74

static func build(kit) -> void:
	kit.meshes["great_dome"] = _dome(kit)
	kit.meshes["great_library"] = _library(kit)
	kit.meshes["great_lighthouse"] = _lighthouse(kit)

static func _column(b: M7MeshBuilder, x: float, z: float, h: float, r: float = 0.013) -> void:
	b.prism(Vector3(x, 0.0, z), r, r * 0.82, h, 7, LIME, LIME_TOP)
	b.prism(Vector3(x, h, z), r * 1.25, r * 1.25, 0.008, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)
	b.prism(Vector3(x, -0.002, z), r * 1.3, r * 1.3, 0.008, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)

# ------------------------------------------------------------------ Great Dome

static func _dome(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1000)
	b.tag = PLAIN
	var drum_r := 0.25
	var drum_h := 0.16
	# a stepped podium all round, wider at the front steps
	b.prism(Vector3(0, 0.0, -0.03), 0.34, 0.33, 0.016, 12, STONE_DARK, STONE, 0.0)
	b.prism(Vector3(0, 0.016, -0.03), 0.31, 0.3, 0.016, 12, STONE, STONE_LIGHT, 0.0)
	b.box(Vector3(0, 0.012, 0.3), Vector3(0.3, 0.024, 0.06), STONE_LIGHT)
	# the drum: concrete, with a band of arched niches round its face
	b.prism(Vector3(0, 0.032, -0.03), drum_r, drum_r * 0.98, drum_h, 12, CONCRETE, LIME_TOP, 0.0)
	for i in 12:
		var a := TAU * (i + 0.5) / 12.0
		var p := Vector3(cos(a) * drum_r * 0.99, 0.032 + drum_h * 0.45, -0.03 + sin(a) * drum_r * 0.99)
		var save := b.xf
		b.xf = b.xf * Transform3D(Basis(Vector3.UP, -a + PI * 0.5), p)
		b.box(Vector3.ZERO, Vector3(0.05, 0.07, 0.012), ARCH)
		b.xf = save
	b.prism(Vector3(0, 0.032 + drum_h, -0.03), drum_r * 1.02, drum_r * 1.02, 0.014, 12, STONE_LIGHT, STONE_LIGHT, 0.0)
	# the dome: a great half-shell, ribbed by a few bands, with a dark oculus and a gold ring
	var dome_base := 0.032 + drum_h + 0.014
	b.blob(Vector3(0, dome_base, -0.03), Vector3(drum_r * 0.98, 0.2, drum_r * 0.98), 12, 5, LIME_TOP, 0.0, LIME)
	for band in [0.25, 0.5, 0.75]:
		var ang: float = float(band) * PI * 0.5
		var r: float = cos(ang) * drum_r * 0.985
		b.prism(Vector3(0, dome_base + sin(ang) * 0.2 - 0.004, -0.03), r, r, 0.008, 12, STONE_LIGHT, STONE_LIGHT, 0.0)
	b.prism(Vector3(0, dome_base + 0.196, -0.03), 0.05, 0.05, 0.01, 8, GOLD, GOLD)
	b.prism(Vector3(0, dome_base + 0.2, -0.03), 0.034, 0.034, 0.006, 8, ARCH, ARCH)
	# the portico: six columns under a pediment, in front of the drum
	for k in 6:
		_column(b, -0.15 + 0.06 * k, 0.27, 0.15)
	b.box(Vector3(0, 0.196, 0.27), Vector3(0.38, 0.018, 0.07), STONE_LIGHT)
	b.tri(Vector3(-0.19, 0.205, 0.306), Vector3(0.19, 0.205, 0.306), Vector3(0, 0.26, 0.306), LIME, Vector3(0, 0.15, -0.2))
	b.tri(Vector3(-0.17, 0.208, 0.307), Vector3(0.17, 0.208, 0.307), Vector3(0, 0.25, 0.307), LIME_TOP, Vector3(0, 0.15, -0.2))
	b.box(Vector3(0, 0.2, 0.19), Vector3(0.34, 0.016, 0.16), STONE)
	b.tag = CIV
	b.box(Vector3(0, 0.188, 0.272), Vector3(0.36, 0.012, 0.068), Color.WHITE)
	# the owner's banner on a pole beside the steps
	var top := Vector3(-0.25, 0.04, 0.34)
	b.pole(top, top + Vector3(0, 0.3, 0), 0.003, WOOD)
	b.quad(top + Vector3(0, 0.3, 0), top + Vector3(0.1, 0.285, 0), top + Vector3(0.1, 0.225, 0), top + Vector3(0, 0.24, 0), Color.WHITE, top + Vector3(0, 0.26, 1.0))
	b.tag = PLAIN
	return b.commit()

# ------------------------------------------------------------------ Great Library

static func _library(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1001)
	b.tag = PLAIN
	var hx := 0.46
	var hz := 0.2
	# the podium and its long steps, the hall, a wing at each end set a little forward
	b.box(Vector3(0, 0.014, 0.01), Vector3(hx * 2.0 + 0.06, 0.028, hz * 2.0 + 0.1), STONE, STONE_LIGHT)
	b.box(Vector3(0, 0.006, hz + 0.11), Vector3(0.5, 0.012, 0.03), STONE_LIGHT)
	b.box(Vector3(0, 0.08, -0.02), Vector3(hx * 2.0 - 0.34, 0.104, hz * 2.0 - 0.1), LIME, LIME_TOP)
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * (hx - 0.1), 0.066, 0.02), Vector3(0.2, 0.076, hz * 2.0 - 0.02), LIME, LIME_TOP)
		b.box(Vector3(sx * (hx - 0.1), 0.108, 0.02), Vector3(0.22, 0.01, hz * 2.0), STONE_LIGHT)
		b.tri(Vector3(sx * (hx - 0.2), 0.113, hz + 0.03), Vector3(sx * (hx - 0.0), 0.113, hz + 0.03), Vector3(sx * (hx - 0.1), 0.15, hz + 0.03), ROOF, Vector3(0, 0.1, -0.2))
		for k in 3:
			_column(b, sx * (hx - 0.18 + 0.08 * k), hz + 0.0, 0.074, 0.011)
	# the colonnade across the middle block, an entablature and the pedimented roof
	for k in 8:
		_column(b, -0.21 + 0.06 * k, hz + 0.045, 0.118)
	b.box(Vector3(0, 0.138, hz + 0.045), Vector3(0.5, 0.018, 0.06), STONE_LIGHT)
	b.tag = CIV
	b.box(Vector3(0, 0.152, hz + 0.045), Vector3(0.5, 0.012, 0.062), Color.WHITE)
	b.tag = PLAIN
	var inner := Vector3(0, 0.1, 0)
	for side in [-1.0, 1.0]:
		var col := ROOF if side > 0.0 else ROOF.darkened(0.12)
		b.quad(Vector3(-0.31, 0.134, (hz - 0.05) * side), Vector3(0.31, 0.134, (hz - 0.05) * side), Vector3(0.31, 0.2, 0), Vector3(-0.31, 0.2, 0), col, inner)
	for sx in [-1.0, 1.0]:
		b.tri(Vector3(sx * 0.31, 0.134, 0.15), Vector3(sx * 0.31, 0.134, -0.15), Vector3(sx * 0.31, 0.2, 0), ROOF_DARK, inner)
	b.tri(Vector3(-0.27, 0.158, hz + 0.076), Vector3(0.27, 0.158, hz + 0.076), Vector3(0, 0.226, hz + 0.076), LIME, Vector3(0, 0.12, -0.3))
	b.tri(Vector3(-0.24, 0.162, hz + 0.078), Vector3(0.24, 0.162, hz + 0.078), Vector3(0, 0.218, hz + 0.078), LIME_TOP, Vector3(0, 0.12, -0.3))
	# a gold scroll medallion on the pediment and a door
	b.prism(Vector3(0, 0.186, hz + 0.082), 0.026, 0.026, 0.004, 10, GOLD, GOLD)
	b.box(Vector3(0, 0.052, hz - 0.01), Vector3(0.06, 0.076, 0.006), ARCH)
	return b.commit()

# ------------------------------------------------------------------ Great Lighthouse

static func _lighthouse(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1002)
	b.tag = PLAIN
	# a stone platform with a seaward-facing wall, then three stages
	b.prism(Vector3(0, -0.02, 0), 0.24, 0.22, 0.06, 4, STONE_DARK, STONE, PI / 4.0)
	b.prism(Vector3(0, 0.04, 0), 0.19, 0.17, 0.02, 4, STONE, STONE_LIGHT, PI / 4.0)
	b.prism(Vector3(0, 0.06, 0), 0.1, 0.084, 0.24, 4, STONE, LIME_TOP, PI / 4.0)
	b.prism(Vector3(0, 0.3, 0), 0.09, 0.09, 0.014, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)
	b.prism(Vector3(0, 0.314, 0), 0.07, 0.06, 0.2, 8, LIME, LIME_TOP, PI / 8.0)
	b.prism(Vector3(0, 0.514, 0), 0.075, 0.075, 0.012, 8, STONE_LIGHT, STONE_LIGHT, PI / 8.0)
	b.prism(Vector3(0, 0.526, 0), 0.052, 0.044, 0.14, 8, LIME, LIME_TOP, 0.0)
	# the gallery under the lantern, a rail, and the lantern room: eight posts round a glowing core, a roof and a finial
	b.prism(Vector3(0, 0.666, 0), 0.07, 0.07, 0.012, 8, STONE_LIGHT, STONE_LIGHT, 0.0)
	b.tag = EMISSIVE
	b.prism(Vector3(0, 0.678, 0), 0.034, 0.034, 0.06, 8, GLASS, GLASS, 0.0)
	b.tag = PLAIN
	for i in 8:
		var a := TAU * i / 8.0
		b.pole(Vector3(cos(a) * 0.044, 0.678, sin(a) * 0.044), Vector3(cos(a) * 0.044, 0.742, sin(a) * 0.044), 0.004, STONE)
	b.prism(Vector3(0, 0.738, 0), 0.058, 0.0, 0.05, 8, ROOF, ROOF, 0.0)
	b.pole(Vector3(0, 0.786, 0), Vector3(0, 0.82, 0), 0.0025, GOLD)
	# the door and slit windows in the base stage
	b.box(Vector3(0, 0.1, 0.092), Vector3(0.04, 0.06, 0.008), ARCH)
	for y in [0.2, 0.26]:
		b.box(Vector3(0, y, 0.088), Vector3(0.012, 0.03, 0.006), ARCH)
	return b.commit()
