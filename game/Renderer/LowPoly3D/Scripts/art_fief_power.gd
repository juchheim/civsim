## Faith & Fief (E5) power and industry: the windmill (NRG-05) and the blast furnace (MET-12), docs/MVP/32 §4.1 and §4.3.
##
## Not wired to the simulation yet (docs/MVP/32 §9); photographed in the Fief gallery (LowPoly3DView.show_fief_gallery). Same units as the rest of the
## kit (1.0 = one tile column, an adult is ~0.1 tall). Both are outer works (docs/MVP/31a): the builder will stand them outside the walls. Buildings
## face +Z (the door, and the tuyere of the furnace, toward the village).
##
##   windmill_body    a tower mill: a stone plinth, a tapering whitewashed tower with a door, a wooden gallery ring and a boat-shaped cap with a tail pole
##                    running down to the ground, and the stub of the windshaft where the sails go. No water anywhere: that is the water mill's.
##   windmill_sails   four lattice sails with cloth, in the XY plane with the hub at the origin and the cloth facing +Z; the rig turns it (placed at the hub:
##                    (0, 0.335, 0.07) above the body's origin)
##   windmill         the body with the sails fixed on it at a quarter turn, for a mill that is not animated
##   blast_furnace       a tall tapering stone stack with a brick crown and a charging ramp up the back, a glowing tuyere arch at its foot, the bellows house
##                       and its wheel, a casting shed with a sand bed of pig iron, a heap of charcoal and a scar of bare earth round it all
##   blast_furnace_cold  the same furnace banked: the arch dark, the bed empty (no fuel, no charcoal, or no power unit)
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const LIME := Color("#e2d6bc")
const LIME_TOP := Color("#f2ead8")
const BRICK := Color("#9a4a34")
const BRICK_DARK := Color("#74382a")
const ROOF := Color("#a8492c")
const ROOF_DARK := Color("#7f3622")
const CAP := Color("#5a4636")
const CAP_LIGHT := Color("#7a6049")
const ARCH := Color("#2a2119")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const SAIL := Color("#efe6d0")
const SAND := Color("#d8c08e")
const IRON := Color("#6e6a66")
const IRON_LIGHT := Color("#8e8a86")
const CHARCOAL := Color("#2b2620")
const SCAR := Color("#5e5042")
const GLOW := Color("#ff9a3c")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE
const CLOTH := M7MeshBuilder.TAG_CLOTH

const HUB := Vector3(0.0, 0.335, 0.07)      # where the sails go, from the body's origin
const SAIL_LEN := 0.2

static func build(kit) -> void:
	kit.meshes["windmill_body"] = _windmill_body(kit)
	kit.meshes["windmill_sails"] = _sails(kit, PI * 0.25, false)
	kit.meshes["windmill"] = _windmill_body(kit, true)
	kit.meshes["blast_furnace"] = _furnace(kit, true)
	kit.meshes["blast_furnace_cold"] = _furnace(kit, false)

# ------------------------------------------------------------------ windmill

## Four lattice sails round the origin in the XY plane, the first at angle `turn`. Each is a stock with a framed, clothed area on its leading side.
static func _sails_into(b: M7MeshBuilder, at: Vector3, turn: float) -> void:
	b.pole(at + Vector3(0, 0, -0.012), at + Vector3(0, 0, 0.018), 0.0075, WOOD)
	for k in 4:
		var a := turn + k * PI * 0.5
		var d := Vector3(cos(a), sin(a), 0.0)
		var nrm := Vector3(-sin(a), cos(a), 0.0)
		# the stock runs right through the hub, so the arm is one long timber with a short tail
		b.pole(at - d * 0.014 + Vector3(0, 0, 0.012), at + d * SAIL_LEN + Vector3(0, 0, 0.012), 0.0036, WOOD_LIGHT)
		var s0 := 0.045
		var w := 0.052
		var z := 0.0105
		# the frame: two long rails and rungs, the cloth stretched between them on the leading side
		b.pole(at + d * s0 + nrm * 0.006 + Vector3(0, 0, z), at + d * SAIL_LEN + nrm * 0.006 + Vector3(0, 0, z), 0.0018, WOOD)
		b.pole(at + d * s0 + nrm * w + Vector3(0, 0, z), at + d * SAIL_LEN + nrm * w + Vector3(0, 0, z), 0.0018, WOOD)
		for r in 5:
			var s := s0 + (SAIL_LEN - s0) * r / 4.0
			b.pole(at + d * s + nrm * 0.006 + Vector3(0, 0, z), at + d * s + nrm * w + Vector3(0, 0, z), 0.0016, WOOD)
		var tag := b.tag
		b.tag = PLAIN   # a fixed cream, not the owner's tint (the cloth tag would colour it)
		var p0 := at + d * (s0 + 0.004) + nrm * 0.009 + Vector3(0, 0, z + 0.001)
		var p1 := at + d * (SAIL_LEN - 0.004) + nrm * 0.009 + Vector3(0, 0, z + 0.001)
		var p2 := at + d * (SAIL_LEN - 0.004) + nrm * (w - 0.003) + Vector3(0, 0, z + 0.001)
		var p3 := at + d * (s0 + 0.004) + nrm * (w - 0.003) + Vector3(0, 0, z + 0.001)
		b.quad(p0, p1, p2, p3, SAIL, p0 + Vector3(0, 0, 1.0))
		b.quad(p0, p1, p2, p3, SAIL.darkened(0.12), p0 - Vector3(0, 0, 1.0))
		b.tag = tag

static func _sails(kit, turn: float, _fixed: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1131)
	b.tag = PLAIN
	_sails_into(b, Vector3.ZERO, turn)
	return b.commit()

static func _windmill_body(kit, with_sails: bool = false) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1130 if not with_sails else 1132)
	b.tag = PLAIN
	# a stone plinth, the tower tapering in eight whitewashed faces (a face is centred on +Z): twice as tall as it is wide, a string course and a gallery
	b.prism(Vector3(0, -0.01, 0), 0.108, 0.094, 0.05, 8, STONE_DARK, STONE, PI / 8.0)
	b.prism(Vector3(0, 0.03, 0), 0.092, 0.05, 0.26, 8, LIME, LIME_TOP, PI / 8.0)
	b.prism(Vector3(0, 0.1, 0), 0.08, 0.08, 0.006, 8, STONE_LIGHT, STONE_LIGHT, PI / 8.0)
	b.prism(Vector3(0, 0.18, 0), 0.07, 0.07, 0.007, 8, WOOD_LIGHT, WOOD_LIGHT, PI / 8.0)
	for k in 8:
		var a := TAU * k / 8.0 + PI / 8.0
		b.pole(Vector3(cos(a) * 0.066, 0.187, sin(a) * 0.066), Vector3(cos(a) * 0.066, 0.2, sin(a) * 0.066), 0.0018, WOOD)
	# the door (a dark arch with a timber lintel) and two stone steps before it, a loading hatch above, slit windows on the faces either side
	b.box(Vector3(0, 0.036, 0.083), Vector3(0.03, 0.054, 0.012), ARCH)
	b.box(Vector3(0, 0.066, 0.083), Vector3(0.038, 0.007, 0.014), WOOD)
	b.box(Vector3(0, 0.006, 0.102), Vector3(0.05, 0.012, 0.02), STONE_LIGHT)
	b.box(Vector3(0, 0.0, 0.112), Vector3(0.06, 0.008, 0.02), STONE)
	b.box(Vector3(0, 0.145, 0.0615), Vector3(0.02, 0.026, 0.01), ARCH)
	for k in [-1.0, 1.0]:
		var a2: float = PI * 0.5 + k * PI * 0.25
		b.box(Vector3(cos(a2) * 0.0665, 0.135, sin(a2) * 0.0665), Vector3(0.007, 0.026, 0.007), ARCH)
		b.box(Vector3(cos(a2) * 0.0555, 0.225, sin(a2) * 0.0555), Vector3(0.006, 0.022, 0.006), ARCH)
	# the cap: a timber ring, a boat-shaped roof and a finial
	b.prism(Vector3(0, 0.288, 0), 0.054, 0.062, 0.012, 8, CAP_LIGHT, CAP, PI / 8.0)
	b.prism(Vector3(0, 0.3, 0), 0.066, 0.0, 0.066, 8, CAP, CAP_LIGHT, PI / 8.0)
	b.pole(Vector3(0, 0.364, 0), Vector3(0, 0.384, 0), 0.0028, WOOD)
	# the tail pole with its crossbar, running down behind the mill to the ground
	b.pole(Vector3(0, 0.3, -0.05), Vector3(0, 0.014, -0.22), 0.0048, WOOD)
	b.pole(Vector3(-0.03, 0.06, -0.205), Vector3(0.03, 0.06, -0.205), 0.0035, WOOD_LIGHT)
	b.pole(Vector3(0, 0.014, -0.22), Vector3(0, 0.0, -0.234), 0.0048, WOOD)
	# the windshaft through the cap's front, and a stack of grain sacks beside the door
	b.pole(Vector3(0, HUB.y, 0.0), Vector3(0, HUB.y, HUB.z), 0.0075, WOOD)
	for k in 4:
		var sx := 0.054 + 0.022 * (k % 2)
		b.blob(Vector3(sx, 0.012 + 0.014 * (k / 2), 0.108 + 0.006 * (k / 2)), Vector3(0.016, 0.014, 0.012), 5, 2, Color("#d6c7a3"), 0.1, Color("#b9a982"))
	if with_sails:
		_sails_into(b, HUB, PI * 0.25)
	return b.commit()

# ------------------------------------------------------------------ blast furnace

static func _furnace(kit, hot: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1140 + (0 if hot else 1))
	b.tag = PLAIN
	# the scar: the ground burnt bare round the works, a darker ring where the charcoal is kept
	b.prism(Vector3(0, -0.004, 0), 0.31, 0.3, 0.008, 12, SCAR.lightened(0.08), SCAR, PI / 12.0)
	# the stack: a battered stone foot and a tall square shaft tapering to a brick crown, stone courses picked out along its height
	b.prism(Vector3(0, -0.02, 0), 0.128, 0.108, 0.07, 4, STONE_DARK, STONE_DARK, PI / 4.0)
	b.prism(Vector3(0, 0.05, 0), 0.108, 0.07, 0.25, 4, STONE, STONE_LIGHT, PI / 4.0)
	for k in 4:
		var y := 0.1 + 0.055 * k
		var r := lerpf(0.108, 0.07, (y - 0.05) / 0.25) * 1.02
		b.prism(Vector3(0, y, 0), r, r, 0.008, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)
	b.prism(Vector3(0, 0.3, 0), 0.08, 0.074, 0.03, 4, BRICK, BRICK_DARK, PI / 4.0)
	b.prism(Vector3(0, 0.328, 0), 0.056, 0.056, 0.004, 4, ARCH, ARCH, PI / 4.0)
	if hot:
		b.tag = EMISSIVE
		b.prism(Vector3(0, 0.3285, 0), 0.046, 0.046, 0.002, 4, GLOW.darkened(0.35), GLOW.darkened(0.35), PI / 4.0)
		b.tag = PLAIN
	# the charging ramp up the back (-Z) to the top, timber on two trestles, with a barrow of charcoal at its foot
	b.pole(Vector3(-0.022, 0.012, -0.3), Vector3(-0.022, 0.316, -0.06), 0.006, WOOD)
	b.pole(Vector3(0.022, 0.012, -0.3), Vector3(0.022, 0.316, -0.06), 0.006, WOOD)
	for k in 6:
		var t := (k + 0.5) / 6.0
		b.box(Vector3(0, lerpf(0.016, 0.318, t), lerpf(-0.3, -0.06, t)), Vector3(0.056, 0.004, 0.044), WOOD_LIGHT)
	for t in [0.35, 0.7]:
		var rz: float = lerpf(-0.3, -0.06, t)
		var ry: float = lerpf(0.016, 0.318, t)
		for sx in [-0.026, 0.026]:
			b.pole(Vector3(sx, 0.0, rz), Vector3(sx, ry - 0.004, rz), 0.004, WOOD)
	# the tuyere arch at the foot of the front face: a dark recess with the fire glowing in it, and the blast pipe running in from the bellows
	b.box(Vector3(0, 0.044, 0.0745), Vector3(0.058, 0.056, 0.014), ARCH)
	b.box(Vector3(0, 0.07, 0.0775), Vector3(0.07, 0.008, 0.016), STONE_LIGHT)
	if hot:
		b.tag = EMISSIVE
		b.box(Vector3(0, 0.042, 0.082), Vector3(0.046, 0.04, 0.006), GLOW)
		b.box(Vector3(0, 0.026, 0.092), Vector3(0.05, 0.006, 0.02), GLOW.darkened(0.15))
		b.tag = PLAIN
	b.pole(Vector3(0.1, 0.05, 0.0), Vector3(0.014, 0.048, 0.07), 0.0045, IRON)
	# the bellows house on the +X side with its water wheel: a timber box under a pitched roof, a wheel with spokes on the outside face
	b.box(Vector3(0.17, 0.034, 0.0), Vector3(0.08, 0.068, 0.1), WOOD_LIGHT, WOOD_LIGHT.lightened(0.08))
	for sz in [-1.0, 1.0]:
		b.quad(Vector3(0.126, 0.068, sz * 0.054), Vector3(0.214, 0.068, sz * 0.054), Vector3(0.214, 0.1, 0.0), Vector3(0.126, 0.1, 0.0),
			ROOF if sz > 0.0 else ROOF.darkened(0.12), Vector3(0.17, 0.05, 0.0))
	for sx in [0.126, 0.214]:
		b.tri(Vector3(sx, 0.068, 0.054), Vector3(sx, 0.068, -0.054), Vector3(sx, 0.1, 0.0), ROOF_DARK, Vector3(0.17, 0.05, 0.0))
	var wc := Vector3(0.226, 0.058, 0.0)
	for i in 8:
		var a0 := TAU * i / 8.0
		var a1 := TAU * (i + 1) / 8.0
		b.pole(wc + Vector3(0, sin(a0) * 0.046, cos(a0) * 0.046), wc + Vector3(0, sin(a1) * 0.046, cos(a1) * 0.046), 0.0045, WOOD_LIGHT)
	for a in [0.0, PI * 0.25, PI * 0.5, PI * 0.75]:
		b.pole(wc + Vector3(0, sin(a) * 0.046, cos(a) * 0.046), wc - Vector3(0, sin(a) * 0.046, cos(a) * 0.046), 0.0026, WOOD)
	b.pole(wc + Vector3(-0.02, 0, 0), wc + Vector3(0.014, 0, 0), 0.005, WOOD)
	# the casting shed in front: four posts, a lean-to roof, a sand bed in front of the arch with the pig iron lying in its channels
	for sx in [-0.08, 0.08]:
		for sz in [0.14, 0.24]:
			b.pole(Vector3(sx, 0.0, sz), Vector3(sx, 0.07 - 0.012 * (sz - 0.14) / 0.1, sz), 0.0045, WOOD)
	b.quad(Vector3(-0.1, 0.076, 0.12), Vector3(0.1, 0.076, 0.12), Vector3(0.1, 0.056, 0.26), Vector3(-0.1, 0.056, 0.26), ROOF, Vector3(0, 0.0, 0.2))
	b.quad(Vector3(-0.1, 0.073, 0.12), Vector3(0.1, 0.073, 0.12), Vector3(0.1, 0.053, 0.26), Vector3(-0.1, 0.053, 0.26), ROOF_DARK, Vector3(0, 0.2, 0.2))
	b.box(Vector3(0, 0.004, 0.18), Vector3(0.17, 0.008, 0.16), SAND)
	if hot:
		for k in 5:
			b.box(Vector3(-0.064 + 0.032 * k, 0.012, 0.19), Vector3(0.012, 0.007, 0.1), IRON if k % 2 == 0 else IRON_LIGHT)
		b.box(Vector3(0, 0.012, 0.15), Vector3(0.14, 0.007, 0.012), IRON_LIGHT)
	else:
		for k in 5:
			b.box(Vector3(-0.064 + 0.032 * k, 0.009, 0.19), Vector3(0.008, 0.003, 0.1), SAND.darkened(0.18))
	# a stack of cast pigs beside the shed, the charcoal heap on the other side (a big dark mound under a skin of earth, and its wood stack)
	for k in 6:
		b.box(Vector3(0.13 + 0.01 * (k % 2), 0.008 + 0.01 * (k / 2), 0.2), Vector3(0.05, 0.01, 0.016), IRON if k % 2 == 0 else IRON_LIGHT)
	if hot:
		b.blob(Vector3(-0.2, 0.0, 0.1), Vector3(0.1, 0.055, 0.085), 8, 4, CHARCOAL, 0.12, CHARCOAL.lightened(0.05))
		b.blob(Vector3(-0.14, 0.0, 0.2), Vector3(0.07, 0.04, 0.06), 7, 3, CHARCOAL.lightened(0.06), 0.15, CHARCOAL)
	else:
		b.blob(Vector3(-0.2, 0.0, 0.1), Vector3(0.1, 0.04, 0.09), 7, 3, CHARCOAL.lightened(0.04), 0.12, CHARCOAL)
	for k in 5:
		b.pole(Vector3(-0.24 + 0.014 * k, 0.012, -0.12), Vector3(-0.24 + 0.014 * k, 0.012, -0.2), 0.006, WOOD if k % 2 == 0 else WOOD_LIGHT)
	return b.commit()
