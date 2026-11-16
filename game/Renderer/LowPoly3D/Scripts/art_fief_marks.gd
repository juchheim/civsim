## Faith & Fief (E5) route markers: the pilgrimage post (BEL-11) and the quarantine gate (MED-08), docs/MVP/32 §7 and §8.4.
##
## Not wired to the simulation yet (docs/MVP/32 §9: `pilgrimRoutes` and `quarantine` list the routes and borders concerned); photographed in the Fief
## gallery. Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.06 tall). Both are small props set on a road or a border, not
## buildings: the pilgrimage post at each end of a completed, traveled pilgrimage route (docs/MVP/32 §7: only on roads pilgrims actually use), the
## quarantine gate across the road or track where quarantine is in force. A road runs along local X through the gate.
##
##   pilgrim_marker    a way-post: a cairn base, a tapered stone shaft with a gold scallop badge on its face and a wooden arm pointing along the route (+X),
##                     a lantern hook at the top; a cluster of pilgrims' staves leans against it
##   quarantine_gate   a barrier across the road: two posts, a long striped bar lowered across it, a yellow flag on a tall pole, a lantern and a small sign
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const GOLD := Color("#c9973c")
const IRON := Color("#4a4f57")
const YELLOW := Color("#e6c230")
const BLACK := Color("#2a2119")
const LAMP := Color("#ffd27a")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE

static func build(kit) -> void:
	kit.meshes["pilgrim_marker"] = _marker(kit)
	kit.meshes["quarantine_gate"] = _gate(kit)

static func _marker(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1610)
	b.tag = PLAIN
	# the cairn: a heap of stones round the foot, each pilgrim adding one
	for k in 7:
		var a := TAU * k / 7.0
		b.blob(Vector3(cos(a) * 0.026, 0.007, sin(a) * 0.026), Vector3(0.012, 0.008, 0.011), 5, 2, STONE_LIGHT if k % 2 == 0 else STONE, 0.2, STONE_DARK)
	# the shaft: a squared, slightly tapering stone post with a cap
	b.prism(Vector3(0, 0.0, 0), 0.026, 0.02, 0.09, 4, STONE, STONE_LIGHT, PI / 4.0)
	b.prism(Vector3(0, 0.09, 0), 0.026, 0.0, 0.016, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)
	# the scallop badge on the front: a gold plate with a fan of ribs and a hinge
	b.box(Vector3(0, 0.06, 0.0215), Vector3(0.03, 0.03, 0.003), STONE_DARK)
	for k in 7:
		var a0 := (k - 3.0) * 0.36 - 0.18
		var a1 := a0 + 0.36
		b.tri(Vector3(0, 0.049, 0.0245), Vector3(sin(a0) * 0.0155, 0.049 + cos(a0) * 0.0155, 0.0245), Vector3(sin(a1) * 0.0155, 0.049 + cos(a1) * 0.0155, 0.0245),
			GOLD if k % 2 == 0 else GOLD.lightened(0.18), Vector3(0, 0.055, 0.0))
	b.box(Vector3(0, 0.0475, 0.0245), Vector3(0.012, 0.004, 0.002), GOLD.darkened(0.25))
	# the arm: a wooden board pointing along the route (+X) with a pointed end
	b.box(Vector3(0.036, 0.078, 0.0), Vector3(0.05, 0.012, 0.004), WOOD_LIGHT)
	b.tri(Vector3(0.061, 0.072, 0.002), Vector3(0.061, 0.084, 0.002), Vector3(0.07, 0.078, 0.002), WOOD_LIGHT, Vector3(0.04, 0.078, -0.1))
	b.tri(Vector3(0.061, 0.072, -0.002), Vector3(0.061, 0.084, -0.002), Vector3(0.07, 0.078, -0.002), WOOD_LIGHT.darkened(0.1), Vector3(0.04, 0.078, 0.1))
	# a lantern hung from an iron hook on the shaft's side
	b.pole(Vector3(-0.02, 0.084, 0.0), Vector3(-0.036, 0.084, 0.0), 0.0018, IRON)
	b.box(Vector3(-0.036, 0.072, 0.0), Vector3(0.01, 0.014, 0.01), IRON)
	b.tag = EMISSIVE
	b.box(Vector3(-0.036, 0.072, 0.0), Vector3(0.007, 0.011, 0.0105), LAMP)
	b.tag = PLAIN
	# a few pilgrims' staves leaned against the shaft
	for k in 3:
		b.pole(Vector3(-0.012 + 0.012 * k, 0.0, 0.034 + 0.002 * k), Vector3(-0.004 + 0.008 * k, 0.062 + 0.004 * k, 0.0215), 0.0022, WOOD_LIGHT)
	return b.commit()

static func _gate(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1611)
	b.tag = PLAIN
	# a worn track through the gate along X, and two posts either side of it with stone footings
	b.box(Vector3(0, 0.002, 0.0), Vector3(0.26, 0.004, 0.06), Color("#8a7254"))
	for sx in [-1.0, 1.0]:
		b.prism(Vector3(sx * 0.09, 0.0, 0.0), 0.012, 0.01, 0.01, 6, STONE, STONE_LIGHT)
		b.pole(Vector3(sx * 0.09, 0.008, 0.0), Vector3(sx * 0.09, 0.07, 0.0), 0.0045, WOOD)
	# the bar across the road: a long pole in alternate yellow and dark bands, lowered, resting in a fork on the far post
	var n := 8
	for k in n:
		var x0 := -0.09 + 0.18 * k / n
		var x1 := -0.09 + 0.18 * (k + 1) / n
		b.pole(Vector3(x0, 0.044, 0.0), Vector3(x1, 0.044, 0.0), 0.0042, YELLOW if k % 2 == 0 else BLACK)
	b.box(Vector3(0.09, 0.047, 0.0), Vector3(0.012, 0.004, 0.012), WOOD)
	b.pole(Vector3(-0.09, 0.044, 0.0), Vector3(-0.112, 0.044, 0.0), 0.0042, BLACK)
	b.blob(Vector3(-0.118, 0.044, 0.0), Vector3(0.007, 0.007, 0.007), 5, 3, IRON, 0.0)
	# the yellow flag on a tall pole by the near post: a square pennant with a dark mark
	b.pole(Vector3(-0.09, 0.07, 0.0), Vector3(-0.09, 0.14, 0.0), 0.0026, WOOD)
	b.quad(Vector3(-0.09, 0.14, 0.0), Vector3(-0.036, 0.132, 0.0), Vector3(-0.036, 0.1, 0.0), Vector3(-0.09, 0.108, 0.0), YELLOW, Vector3(-0.06, 0.12, 1.0))
	b.quad(Vector3(-0.09, 0.14, 0.0), Vector3(-0.036, 0.132, 0.0), Vector3(-0.036, 0.1, 0.0), Vector3(-0.09, 0.108, 0.0), YELLOW.darkened(0.12), Vector3(-0.06, 0.12, -1.0))
	b.blob(Vector3(-0.063, 0.12, 0.0014), Vector3(0.0075, 0.0075, 0.001), 6, 2, BLACK, 0.0)
	b.blob(Vector3(-0.063, 0.12, -0.0014), Vector3(0.0075, 0.0075, 0.001), 6, 2, BLACK, 0.0)
	# a lantern on the far post and a small warning board on a stake beside the track
	b.pole(Vector3(0.09, 0.07, 0.0), Vector3(0.09, 0.078, 0.014), 0.0018, IRON)
	b.box(Vector3(0.09, 0.07, 0.016), Vector3(0.008, 0.01, 0.008), IRON)
	b.tag = EMISSIVE
	b.box(Vector3(0.09, 0.07, 0.016), Vector3(0.0055, 0.0075, 0.0085), LAMP)
	b.tag = PLAIN
	b.pole(Vector3(0.06, 0.0, 0.04), Vector3(0.06, 0.05, 0.04), 0.0024, WOOD)
	b.box(Vector3(0.06, 0.056, 0.04), Vector3(0.03, 0.02, 0.003), Color("#efe6d0"))
	b.box(Vector3(0.06, 0.056, 0.0425), Vector3(0.018, 0.003, 0.001), Color("#b8352c"))
	b.box(Vector3(0.06, 0.056, 0.0425), Vector3(0.003, 0.012, 0.001), Color("#b8352c"))
	return b.commit()
