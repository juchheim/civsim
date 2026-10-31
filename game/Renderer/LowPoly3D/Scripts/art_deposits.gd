## Mineable deposits: one marker per DepositKind (`deposit1` ... `deposit21`, the kind's byte value), drawn on every tile whose deposit the viewer knows.
##
## Art pass: meshes in this file are ADDED to the ones mesh_kit.gd builds (`deposit<kind>_tall` has the long pole for mountain tiles).
## world_builder.gd `_deposits()` places them. Every marker is
## three things at once so a deposit is found at a glance, near or far: an outcrop that shows what the ground holds (boulders, a clay
## pit, ore veins, a salt crust, a coal heap, an oil pool, cane), a patch of stained ground under it, and a tall slim pole topped by a
## glowing gem in the deposit's colour (TAG_EMISSIVE) that stands above the grass, trees and houses.
extends RefCounted

const PIN_H := 0.3        # pole height; the gem sits on top of it
const PIN_H_TALL := 0.85  # on mountain tiles, where peaks stand a tile or two high and would bury a short pole
const GEM_R := 0.06
const PATCH_R := 0.17

# kind -> [outcrop base, outcrop face, gem, style]; kind numbers are CivSim.Sim.World.DepositKind
const KINDS := {
	1: ["#8f8a80", "#b8b3a6", "#e4dfd0", "rock"],         # stone
	2: ["#4b535b", "#8795a3", "#6fb0e6", "rock"],         # flint
	3: ["#9c5a3e", "#b86d4c", "#ee7a4a", "mound"],        # clay
	4: ["#b87a34", "#d99a46", "#ffb02e", "mound"],        # ochre
	5: ["#5a5148", "#d0793f", "#ff8a3a", "ore"],          # native copper
	6: ["#505a52", "#4aa88a", "#ff8a3a", "ore"],          # copper ore (green malachite)
	7: ["#4f4858", "#a35fc8", "#c06cff", "ore"],          # arsenical copper
	8: ["#5b5b5e", "#d9d4c4", "#ffd23a", "ore"],          # tin
	9: ["#6e5f3a", "#f4cc3c", "#ffe14a", "ore"],          # gold
	10: ["#5c6068", "#e4e9f0", "#f4f7ff", "ore"],         # silver
	11: ["#4a423e", "#a8503a", "#e0583a", "ore"],         # iron
	12: ["#bdb8aa", "#f5f2e8", "#fffbe8", "salt"],        # salt
	13: ["#262626", "#46464a", "#aeb4bc", "coal"],        # coal
	14: ["#1b1a22", "#3a3350", "#8a6cf0", "oil"],         # oil
	15: ["#a39a78", "#ece4b4", "#f4ecb0", "crust"],       # saltpeter
	16: ["#b0a040", "#f4e43c", "#fff04a", "crust"],       # sulfur
	17: ["#8c5c4a", "#c98068", "#ff9a7a", "ore"],         # bauxite
	18: ["#4c5a5c", "#46d4b0", "#4cf0c8", "ore"],         # rare earths
	19: ["#8a8ca0", "#cfd4ff", "#c4ccff", "ore"],         # lithium
	20: ["#6e8a3c", "#f2e8f0", "#ff9ee0", "moths"],       # silk moths
	21: ["#5a8a2e", "#9ad04c", "#a6ff4a", "cane"],        # sugarcane
}

static func build(kit) -> void:
	for kind in KINDS:
		_marker(kit, int(kind), KINDS[kind], false)
		_marker(kit, int(kind), KINDS[kind], true)

static func _marker(kit, kind: int, spec: Array, tall: bool) -> void:
	var base := Color(spec[0])
	var face := Color(spec[1])
	# emissive faces are multiplied up in the shader, so a full-strength colour blows out to white: pre-dim it in linear light
	var gem := (Color(spec[2]).srgb_to_linear() * 0.4).linear_to_srgb()
	var style: String = spec[3]
	var b: M7MeshBuilder = kit._b(900 + kind * 13)
	# stained ground: a low disc of the outcrop's colour so even a bare marker reads as a place, not a prop
	b.prism(Vector3(0, -0.004, 0), PATCH_R, PATCH_R * 0.92, 0.014, 9, base.darkened(0.2), base.darkened(0.12), b.rnd() * TAU, 0.1, true)
	match style:
		"rock": _rocks(b, base, face)
		"mound": _mound(b, base, face)
		"ore": _ore(b, base, face)
		"salt": _salt(b, base, face)
		"coal": _coal(b, base, face)
		"oil": _oil(b, base, face)
		"crust": _crust(b, base, face)
		"moths": _moths(b, base, face)
		"cane": _cane(b, base, face)
	_pin(b, gem, PIN_H_TALL if tall else PIN_H)
	kit.meshes["deposit%d%s" % [kind, "_tall" if tall else ""]] = b.commit()

## A boulder: a squat jittered blob, lighter on top.
static func _boulder(b: M7MeshBuilder, p: Vector3, r: Vector3, base: Color, top: Color) -> void:
	b.blob(p + Vector3(0, r.y * 0.55, 0), r, 6, 3, top, 0.16, base)

## A crystal / ore shard: a tilted four-sided chisel standing from p.
static func _shard(b: M7MeshBuilder, p: Vector3, tilt: Vector3, r: float, h: float, col: Color, top: Color) -> void:
	var save := b.xf
	var up := (Vector3.UP + tilt).normalized()
	var side := up.cross(Vector3.FORWARD).normalized()
	var fwd := up.cross(side).normalized()
	b.xf = save * Transform3D(Basis(side, up, fwd), p)
	b.prism(Vector3.ZERO, r, r * 0.25, h, 5, col, top, b.rnd() * TAU)
	b.xf = save

static func _rocks(b: M7MeshBuilder, base: Color, face: Color) -> void:
	_boulder(b, Vector3(-0.02, 0, 0.0), Vector3(0.1, 0.075, 0.085), base, face)
	_boulder(b, Vector3(0.09, 0, 0.05), Vector3(0.06, 0.05, 0.055), base.darkened(0.1), face)
	_boulder(b, Vector3(-0.09, 0, 0.07), Vector3(0.05, 0.04, 0.05), base, face.lightened(0.05))
	_boulder(b, Vector3(0.02, 0, -0.09), Vector3(0.055, 0.045, 0.05), base.darkened(0.06), face)

static func _mound(b: M7MeshBuilder, base: Color, face: Color) -> void:
	# a dug heap with the pit beside it
	b.blob(Vector3(-0.03, 0.01, 0.0), Vector3(0.13, 0.07, 0.11), 8, 3, face, 0.1, base)
	b.blob(Vector3(0.07, 0.01, 0.07), Vector3(0.07, 0.04, 0.06), 6, 3, face.lightened(0.06), 0.12, base)
	b.prism(Vector3(0.05, 0.008, -0.07), 0.055, 0.04, 0.006, 8, base.darkened(0.45), base.darkened(0.5))

static func _ore(b: M7MeshBuilder, base: Color, face: Color) -> void:
	# a dark rock mass splitting open on bright veins
	_boulder(b, Vector3(0.0, 0, 0.0), Vector3(0.11, 0.085, 0.095), base, base.lightened(0.12))
	_boulder(b, Vector3(-0.09, 0, 0.06), Vector3(0.055, 0.045, 0.05), base.darkened(0.08), base.lightened(0.06))
	for s in [[-0.03, -0.01, -0.35, 0.3, 0.05, 0.13], [0.05, 0.02, 0.3, -0.2, 0.04, 0.11], [0.0, 0.05, 0.1, 0.45, 0.035, 0.1],
			[0.08, -0.05, 0.5, 0.0, 0.03, 0.085]]:
		_shard(b, Vector3(s[0], 0.045, s[1]), Vector3(s[2], 0, s[3]), s[4], s[5], face, face.lightened(0.28))

static func _salt(b: M7MeshBuilder, base: Color, face: Color) -> void:
	b.blob(Vector3(0, 0.01, 0), Vector3(0.13, 0.04, 0.11), 8, 2, base, 0.08, base.darkened(0.1))
	for s in [[-0.04, 0.0, -0.25, 0.1, 0.05, 0.12], [0.05, 0.03, 0.3, -0.2, 0.045, 0.1], [0.02, -0.05, 0.0, 0.4, 0.04, 0.085],
			[-0.08, 0.05, -0.4, -0.1, 0.035, 0.07]]:
		_shard(b, Vector3(s[0], 0.025, s[1]), Vector3(s[2], 0, s[3]), s[4], s[5], face, Color.WHITE)

static func _coal(b: M7MeshBuilder, base: Color, face: Color) -> void:
	for p in [[0.0, 0.0, 0.08], [0.08, 0.03, 0.06], [-0.08, 0.04, 0.055], [0.03, -0.08, 0.05], [-0.04, -0.05, 0.045], [0.0, 0.07, 0.04]]:
		b.blob(Vector3(p[0], p[2] * 0.6, p[1]), Vector3(p[2], p[2] * 0.85, p[2]), 5, 3, face, 0.2, base)

static func _oil(b: M7MeshBuilder, base: Color, face: Color) -> void:
	b.prism(Vector3(0, 0.004, 0), 0.12, 0.11, 0.012, 10, base, face, 0.0, 0.1, true)
	b.prism(Vector3(0.03, 0.015, 0.01), 0.06, 0.05, 0.004, 8, face.lightened(0.15), face.lightened(0.3))
	_boulder(b, Vector3(-0.1, 0, 0.07), Vector3(0.045, 0.035, 0.04), Color("#5b5750"), Color("#78736a"))
	_boulder(b, Vector3(0.1, 0, -0.07), Vector3(0.04, 0.03, 0.035), Color("#5b5750"), Color("#78736a"))

static func _crust(b: M7MeshBuilder, base: Color, face: Color) -> void:
	b.blob(Vector3(0, 0.01, 0), Vector3(0.13, 0.05, 0.11), 8, 3, face, 0.12, base)
	b.blob(Vector3(0.07, 0.01, 0.05), Vector3(0.06, 0.035, 0.055), 6, 3, face.lightened(0.08), 0.14, base)
	b.blob(Vector3(-0.07, 0.01, -0.04), Vector3(0.05, 0.03, 0.05), 6, 3, face, 0.14, base)

static func _moths(b: M7MeshBuilder, base: Color, face: Color) -> void:
	# a small mulberry with pale cocoons hung in it
	b.prism(Vector3(0, 0, 0), 0.016, 0.011, 0.08, 5, M7MeshKit.BARK, M7MeshKit.BARK)
	b.blob(Vector3(0, 0.115, 0), Vector3(0.1, 0.07, 0.1), 7, 4, base, 0.14, base.darkened(0.25))
	for p in [[0.07, 0.08, 0.02], [-0.06, 0.09, 0.04], [0.01, 0.07, -0.07], [-0.03, 0.12, 0.07]]:
		b.blob(Vector3(p[0], p[1], p[2]), Vector3(0.022, 0.028, 0.022), 5, 3, face)

static func _cane(b: M7MeshBuilder, base: Color, face: Color) -> void:
	for p in [[0.0, 0.0, 0.17], [0.05, 0.03, 0.14], [-0.05, 0.02, 0.15], [0.02, -0.05, 0.12], [-0.03, -0.04, 0.16], [0.07, -0.02, 0.1]]:
		b.pole(Vector3(p[0], 0, p[1]), Vector3(p[0] * 1.2 + 0.01, p[2], p[1] * 1.2), 0.011, base, 5)
		b.blob(Vector3(p[0] * 1.2 + 0.01, p[2], p[1] * 1.2), Vector3(0.03, 0.02, 0.03), 5, 2, face)

## The landmark: a slim pole off to one side of the outcrop with a glowing gem on top.
static func _pin(b: M7MeshBuilder, gem: Color, height: float) -> void:
	var foot := Vector3(0.11, 0.0, -0.06)
	var top := foot + Vector3(0.0, height, 0.0)
	b.pole(foot, top, 0.009, M7MeshKit.WOOD_LIGHT, 5)
	var save := b.tag
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	var c := top + Vector3(0, GEM_R * 1.25, 0)
	var ring := [Vector3(GEM_R, 0, 0), Vector3(0, 0, GEM_R), Vector3(-GEM_R, 0, 0), Vector3(0, 0, -GEM_R)]
	var apex := c + Vector3(0, GEM_R * 1.35, 0)
	var nadir := c - Vector3(0, GEM_R * 1.35, 0)
	for i in 4:
		var p0: Vector3 = c + ring[i]
		var p1: Vector3 = c + ring[(i + 1) % 4]
		var shade := gem if i % 2 == 0 else gem.lightened(0.25)
		b.tri(apex, p0, p1, shade, c)
		b.tri(nadir, p1, p0, shade.darkened(0.12), c)
	b.tag = save
