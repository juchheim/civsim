## Iron Age travel works: the stone bridge (TRN-08) that carries a paved road (TRN-07) over a river, and the harbor (SEA-07).
##
## Laid out by world_builder.gd (`_bridges`, `_harbor`). Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.1 tall).
##
##   bridge    a stone arch bridge along local X, 0.56 long, deck 0.15 wide, ends ramped down to the road; its feet sit below the ground
##   harbor    a quay and two timber piers; the quay is on the land side (+Z) and the piers run out over the water (-Z). Origin at the shoreline
##             at sea level. Built galleys are drawn between the piers when present.
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const PAVING := Color("#b8ad98")
const ARCH := Color("#2a2119")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const WOOD_DARK := Color("#47341f")
const ROPE := Color("#b9a68a")
const CRATE := Color("#a5814f")
const BARREL := Color("#7a5a3a")
const PLAIN := M7MeshBuilder.TAG_PLAIN

static func build(kit) -> void:
	kit.meshes["bridge"] = _bridge(kit)
	kit.meshes["harbor"] = _harbor(kit)

# ------------------------------------------------------------------ bridge

static func _bridge(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(930)
	b.tag = PLAIN
	var half := 0.27
	var w := 0.06
	# the deck: a paved slab on low parapet walls, a little humped in the middle
	b.box(Vector3(0, 0.048, 0), Vector3(half * 2.0 - 0.12, 0.03, w * 2.0), STONE, PAVING)
	b.box(Vector3(0, 0.07, 0), Vector3(half * 2.0 - 0.3, 0.014, w * 2.0 - 0.012), STONE_LIGHT, PAVING)
	for side in [-1.0, 1.0]:
		b.box(Vector3(0, 0.074, side * (w - 0.006)), Vector3(half * 2.0 - 0.12, 0.016, 0.012), STONE_LIGHT)
	# ramps of paving down to the road at each end
	for side in [-1.0, 1.0]:
		var x0: float = side * (half - 0.06)
		var x1: float = side * half
		b.quad(Vector3(x0, 0.063, -w), Vector3(x0, 0.063, w), Vector3(x1, 0.004, w), Vector3(x1, 0.004, -w), PAVING, Vector3((x0 + x1) * 0.5, -0.1, 0.0))
	# abutments and the middle pier, with two dark arches between them
	for ax in [-0.17, 0.0, 0.17]:
		b.box(Vector3(ax, -0.015, 0), Vector3(0.045 if ax == 0.0 else 0.055, 0.1, w * 2.0), STONE_DARK, STONE)
	for ax in [-0.085, 0.085]:
		for sz in [-1.0, 1.0]:
			b.box(Vector3(ax, 0.015, sz * (w + 0.002)), Vector3(0.1, 0.05, 0.004), ARCH)
			b.box(Vector3(ax, 0.042, sz * (w + 0.003)), Vector3(0.112, 0.008, 0.004), STONE_LIGHT)
	return b.commit()

# ------------------------------------------------------------------ harbor

static func _post(b: M7MeshBuilder, x: float, z: float, top: float) -> void:
	b.pole(Vector3(x, -0.14, z), Vector3(x, top, z), 0.011, WOOD_DARK, 5)

static func _harbor(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(940)
	b.tag = PLAIN
	# the quay: a stone-faced platform along the shore, paved on top, with bollards
	b.box(Vector3(0, 0.03, 0.04), Vector3(0.98, 0.12, 0.2), STONE, PAVING)
	b.box(Vector3(0, 0.095, -0.05), Vector3(0.98, 0.012, 0.018), STONE_LIGHT)
	for bx in [-0.4, -0.13, 0.13, 0.4]:
		b.prism(Vector3(bx, 0.09, -0.03), 0.012, 0.009, 0.026, 5, WOOD, WOOD_LIGHT)
	# two piers out over the water: plank decks on piles
	for px in [-0.37, 0.37]:
		b.box(Vector3(px, 0.052, -0.4), Vector3(0.075, 0.016, 0.62), WOOD_LIGHT, WOOD_LIGHT.lightened(0.06))
		for k in 5:
			var z := -0.1 - 0.13 * k
			_post(b, px - 0.036, z, 0.075)
			_post(b, px + 0.036, z, 0.075)
		# a cross-plank every so often
		for k in 8:
			b.box(Vector3(px, 0.062, -0.12 - 0.07 * k), Vector3(0.082, 0.004, 0.012), WOOD)
	# a short walkway joins the piers across the water end
	b.box(Vector3(0, 0.052, -0.7), Vector3(0.8, 0.014, 0.05), WOOD_LIGHT, WOOD_LIGHT.lightened(0.05))
	# cargo on the quay: crates, barrels and a stacked pile
	for i in 3:
		b.box(Vector3(-0.42 + 0.075 * i, 0.12, 0.07), Vector3(0.06, 0.05, 0.06), CRATE, CRATE.lightened(0.08))
	b.box(Vector3(-0.38, 0.17, 0.07), Vector3(0.05, 0.05, 0.05), CRATE.darkened(0.08), CRATE.lightened(0.1))
	for i in 3:
		b.prism(Vector3(0.3 + 0.06 * i, 0.09, 0.08), 0.022, 0.02, 0.05, 6, BARREL, WOOD_LIGHT)
	# a jib crane on the quay for loading: post, arm and a rope with a hook
	b.pole(Vector3(0.0, 0.09, 0.08), Vector3(0.0, 0.3, 0.08), 0.012, WOOD)
	b.pole(Vector3(0.0, 0.28, 0.08), Vector3(0.0, 0.31, -0.12), 0.008, WOOD_LIGHT)
	b.pole(Vector3(0.0, 0.25, 0.08), Vector3(0.0, 0.3, -0.07), 0.005, WOOD)
	b.pole(Vector3(0.0, 0.31, -0.115), Vector3(0.0, 0.17, -0.115), 0.0025, ROPE)
	b.box(Vector3(0.0, 0.16, -0.115), Vector3(0.018, 0.018, 0.018), STONE_DARK)
	return b.commit()
