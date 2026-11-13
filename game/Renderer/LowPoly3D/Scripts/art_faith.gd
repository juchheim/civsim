## Faith and rule in the Iron Age: the colonnaded temple (STN-12 Ashlar & Columns), the standards a spreading faith (BEL-09) plants, and the standards of a
## province seat (TER-08) and of the feudal contract (GOV-09).
##
## Laid out by world_builder.gd (`_place_civic` for the temple, `_faith_standards`, `_province_standards`). Same units as the rest of the kit (1.0 = one tile
## column, an adult is ~0.1 tall). Everything faces +Z; TAG_CIV cloth takes the colour of the instance it is placed with (the faith's own civilization for a
## faith standard, the owner for the others).
##
##   temple_tier2       a peristyle temple: a stepped podium, a whitewashed cella ringed by columns, an entablature with the owner's frieze and a pedimented roof
##   colonnade          STN-12: the stone colonnade a Temple town adds beside its temple: a stylobate, six columns carrying a lintel with the owner's frieze, a tiled gable
##                      roof over a plastered back wall; runs along local X and is symmetric front to back, so the builder can turn it either way
##   faith_standard     a cairn with a tall pole and a long pennant in the colour of the faith's civilization: one planted for each share of a town that follows it
##   province_standard  a plinth with a seal, a tall pole with two stacked pennants and a gold knob: the seat of a governor's province
##   feudal_banner      a short pole and crossbar with a hanging banner marked with a chevron: a town under the feudal contract
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const LIME := Color("#e2d6bc")
const LIME_TOP := Color("#f2ead8")
const ROOF := Color("#a8492c")
const ROOF_DARK := Color("#7f3622")
const ARCH := Color("#2a2119")
const WOOD := Color("#5e4631")
const GOLD := Color("#c9973c")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV

static func build(kit) -> void:
	kit.meshes["temple_tier2"] = _temple(kit)
	kit.meshes["colonnade"] = _colonnade(kit)
	kit.meshes["faith_standard"] = _faith_standard(kit)
	kit.meshes["province_standard"] = _province_standard(kit)
	kit.meshes["feudal_banner"] = _feudal_banner(kit)

static func _column(b: M7MeshBuilder, x: float, z: float, h: float) -> void:
	b.prism(Vector3(x, 0.0, z), 0.011, 0.009, h, 7, LIME, LIME_TOP)
	b.prism(Vector3(x, h, z), 0.014, 0.014, 0.007, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)
	b.prism(Vector3(x, -0.002, z), 0.014, 0.014, 0.007, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)

static func _temple(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1010)
	b.tag = PLAIN
	var hx := 0.17
	var hz := 0.23
	# a podium of three courses with a stair at the front, the cella inside the colonnade, a door in its front wall
	b.box(Vector3(0, 0.008, 0), Vector3(hx * 2.0 + 0.05, 0.016, hz * 2.0 + 0.05), STONE_DARK, STONE)
	b.box(Vector3(0, 0.022, 0), Vector3(hx * 2.0 + 0.02, 0.012, hz * 2.0 + 0.02), STONE, STONE_LIGHT)
	for k in 3:
		b.box(Vector3(0, 0.004 + 0.007 * k, hz + 0.05 - 0.012 * k), Vector3(0.2 - 0.02 * k, 0.008, 0.02), STONE_LIGHT)
	b.box(Vector3(0, 0.09, 0.0), Vector3(0.2, 0.108, 0.34), LIME, LIME_TOP)
	b.box(Vector3(0, 0.07, hz - 0.059), Vector3(0.05, 0.066, 0.004), ARCH)
	# the colonnade: four columns across each end and four down each side
	var xs := [-0.12, -0.04, 0.04, 0.12]
	for x in xs:
		_column(b, x, hz - 0.01, 0.112)
		_column(b, x, -hz + 0.01, 0.112)
	for z in [-0.075, 0.075]:
		_column(b, -hx + 0.02, z, 0.112)
		_column(b, hx - 0.02, z, 0.112)
	# the entablature, the owner's frieze on the front, and the pedimented roof (ridge front to back)
	b.box(Vector3(0, 0.124, 0.0), Vector3(hx * 2.0 - 0.0, 0.016, hz * 2.0), STONE_LIGHT, LIME_TOP)
	b.tag = CIV
	b.box(Vector3(0, 0.137, hz - 0.0), Vector3(hx * 2.0, 0.01, 0.01), Color.WHITE)
	b.tag = PLAIN
	var inner := Vector3(0, 0.12, 0)
	for side in [-1.0, 1.0]:
		var col := ROOF if side > 0.0 else ROOF.darkened(0.12)
		b.quad(Vector3((hx - 0.01) * side, 0.132, -hz + 0.01), Vector3((hx - 0.01) * side, 0.132, hz - 0.01), Vector3(0, 0.19, hz - 0.01), Vector3(0, 0.19, -hz + 0.01), col, inner)
	for sz in [-1.0, 1.0]:
		b.tri(Vector3(-hx + 0.01, 0.132, (hz - 0.01) * sz), Vector3(hx - 0.01, 0.132, (hz - 0.01) * sz), Vector3(0, 0.19, (hz - 0.01) * sz), LIME, inner)
	b.prism(Vector3(0, 0.152, hz - 0.003), 0.02, 0.02, 0.003, 10, GOLD, GOLD)
	return b.commit()

static func _colonnade(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1014)
	b.tag = PLAIN
	var hx := 0.23
	var hz := 0.06
	# a stylobate of two courses, the back wall in limewash with three shallow recesses, and a row of six columns along each face
	b.box(Vector3(0, 0.008, 0), Vector3(hx * 2.0 + 0.03, 0.016, hz * 2.0 + 0.03), STONE_DARK, STONE)
	b.box(Vector3(0, 0.019, 0), Vector3(hx * 2.0 + 0.01, 0.01, hz * 2.0 + 0.01), STONE, STONE_LIGHT)
	b.box(Vector3(0, 0.07, 0), Vector3(hx * 2.0 - 0.02, 0.1, 0.018), LIME, LIME_TOP)
	for sx in [-0.14, 0.0, 0.14]:
		for sz in [-1.0, 1.0]:
			b.box(Vector3(sx, 0.058, sz * 0.0095), Vector3(0.05, 0.062, 0.002), ARCH)
	for k in 6:
		var x := -0.175 + 0.07 * k
		for sz in [-1.0, 1.0]:
			_column(b, x, sz * (hz - 0.012), 0.098)
	# the entablature across the top: a lintel course, the owner's frieze on both faces, and a tiled gable above it, closed by a plain pediment at each end
	b.box(Vector3(0, 0.121, 0), Vector3(hx * 2.0 + 0.01, 0.016, hz * 2.0 - 0.004), STONE_LIGHT, LIME_TOP)
	b.tag = CIV
	for sz in [-1.0, 1.0]:
		b.box(Vector3(0, 0.134, sz * (hz - 0.004)), Vector3(hx * 2.0 + 0.01, 0.01, 0.008), Color.WHITE)
	b.tag = PLAIN
	var inner := Vector3(0, 0.12, 0)
	for side in [-1.0, 1.0]:
		var col := ROOF if side > 0.0 else ROOF.darkened(0.12)
		b.quad(Vector3(-hx - 0.008, 0.139, (hz + 0.006) * side), Vector3(hx + 0.008, 0.139, (hz + 0.006) * side), Vector3(hx + 0.008, 0.176, 0), Vector3(-hx - 0.008, 0.176, 0), col, inner)
	for sx in [-1.0, 1.0]:
		b.tri(Vector3(sx * (hx + 0.008), 0.139, hz + 0.006), Vector3(sx * (hx + 0.008), 0.139, -hz - 0.006), Vector3(sx * (hx + 0.008), 0.176, 0), LIME, inner)
	return b.commit()

static func _faith_standard(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1011)
	b.tag = PLAIN
	for i in 5:
		var a := TAU * i / 5.0
		b.blob(Vector3(cos(a) * 0.02, 0.01, sin(a) * 0.02), Vector3(0.017, 0.012, 0.015), 5, 3, STONE_LIGHT, 0.15, STONE)
	b.pole(Vector3(0, 0.0, 0), Vector3(0, 0.3, 0), 0.0032, WOOD)
	b.prism(Vector3(0, 0.3, 0), 0.006, 0.0, 0.016, 5, GOLD, GOLD)
	b.tag = CIV
	var top := Vector3(0, 0.29, 0)
	b.quad(top, top + Vector3(0, -0.012, 0.09), top + Vector3(0, -0.05, 0.075), top + Vector3(0, -0.06, 0), Color.WHITE, top + Vector3(1.0, -0.03, 0))
	b.tag = PLAIN
	b.prism(Vector3(0, 0.236, 0.0), 0.007, 0.007, 0.003, 6, GOLD, GOLD)
	return b.commit()

static func _province_standard(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1012)
	b.tag = PLAIN
	b.prism(Vector3(0, 0.0, 0), 0.05, 0.044, 0.03, 6, STONE, STONE_LIGHT)
	b.prism(Vector3(0, 0.03, 0), 0.03, 0.03, 0.004, 10, GOLD, GOLD)
	b.pole(Vector3(0, 0.03, 0), Vector3(0, 0.46, 0), 0.004, WOOD)
	b.blob(Vector3(0, 0.47, 0), Vector3(0.011, 0.011, 0.011), 6, 3, GOLD, 0.0)
	b.tag = CIV
	for k in 2:
		var top := Vector3(0, 0.44 - 0.1 * k, 0)
		b.quad(top, top + Vector3(0, -0.01, 0.12), top + Vector3(0, -0.045, 0.1), top + Vector3(0, -0.07, 0), Color.WHITE, top + Vector3(1.0, -0.03, 0))
		b.tri(top + Vector3(0, -0.07, 0), top + Vector3(0, -0.045, 0.1), top + Vector3(0, -0.082, 0.062), Color.WHITE, top + Vector3(1.0, -0.05, 0))
	b.tag = PLAIN
	return b.commit()

static func _feudal_banner(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1013)
	b.tag = PLAIN
	b.pole(Vector3(0, 0.0, 0), Vector3(0, 0.34, 0), 0.0032, WOOD)
	b.pole(Vector3(-0.05, 0.32, 0), Vector3(0.05, 0.32, 0), 0.0026, WOOD)
	b.blob(Vector3(-0.052, 0.32, 0), Vector3(0.005, 0.005, 0.005), 5, 3, GOLD, 0.0)
	b.blob(Vector3(0.052, 0.32, 0), Vector3(0.005, 0.005, 0.005), 5, 3, GOLD, 0.0)
	b.tag = CIV
	b.quad(Vector3(-0.045, 0.318, 0.004), Vector3(0.045, 0.318, 0.004), Vector3(0.045, 0.19, 0.004), Vector3(-0.045, 0.19, 0.004), Color.WHITE, Vector3(0, 0.25, -1.0))
	b.tri(Vector3(-0.045, 0.19, 0.004), Vector3(0.045, 0.19, 0.004), Vector3(0, 0.162, 0.004), Color.WHITE, Vector3(0, 0.25, -1.0))
	b.tag = PLAIN
	# a chevron in pale cloth and a gold ring
	b.quad(Vector3(-0.04, 0.29, 0.006), Vector3(0, 0.25, 0.006), Vector3(0, 0.236, 0.006), Vector3(-0.04, 0.276, 0.006), LIME_TOP, Vector3(0, 0.25, -1.0))
	b.quad(Vector3(0.04, 0.29, 0.006), Vector3(0, 0.25, 0.006), Vector3(0, 0.236, 0.006), Vector3(0.04, 0.276, 0.006), LIME_TOP, Vector3(0, 0.25, -1.0))
	return b.commit()
