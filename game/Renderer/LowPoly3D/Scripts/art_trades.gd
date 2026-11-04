## Iron Age land and sea works: the terraced field (AGR-08), the press (MEC-05) and windlass well (MEC-04), grafted vineyards and olive groves (FOR-08) and
## the war galley (SEA-09).
##
## Laid out by world_builder.gd (the plot loop for terraces and orchards, `_place_civic` for the press and well, `_harbor` and `_blockade` for galleys). Same
## units as the rest of the kit (1.0 = one tile column, an adult is ~0.1 tall). Land meshes face +Z; ships run along local Z, the bow at +Z.
##
##   terrace_field   a hillside tile cut into three stepped fields behind stone retaining walls, crops in rows on each
##   vineyard        a tile of trellised vine rows with posts, wires, leaf and purple grape clusters
##   olive_tree      a squat, gnarled grey-green tree on a twisted trunk (an olive grove draws seven of them)
##   press           a stone-floored shed under a roof with a screw press, a barrel and a stack of baskets
##   windlass_well   a stone ring well under a timber frame with a crank, rope and bucket
##   galley          a long oared warship: a bronze-shod ram, a raised stern, two banks of oars, shields along the rail in the owner's colour, a furled sail
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const SOIL := Color("#7a5a3c")
const LEAF := Color("#6f9a34")
const LEAF_DARK := Color("#4f7a2a")
const GRAPE := Color("#5a2f52")
const OLIVE := Color("#7f8a5a")
const OLIVE_DARK := Color("#5f6a40")
const TRUNK := Color("#5a4632")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const ROOF := Color("#a8492c")
const ROOF_DARK := Color("#7f3622")
const ROPE := Color("#b9a68a")
const BASKET := Color("#b08a4e")
const PLANK_LOW := Color("#8a6238")
const PLANK_UP := Color("#b88a56")
const BRONZE := Color("#c9973c")
const SAIL := Color("#e8dcc0")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const CROP := M7MeshBuilder.TAG_CROP
const FOLIAGE := M7MeshBuilder.TAG_FOLIAGE

static func build(kit) -> void:
	kit.meshes["terrace_field"] = _terrace(kit)
	kit.meshes["vineyard"] = _vineyard(kit)
	kit.meshes["olive_tree"] = _olive(kit)
	kit.meshes["press"] = _press(kit)
	kit.meshes["windlass_well"] = _well(kit)
	kit.meshes["galley"] = _galley(kit)

# ------------------------------------------------------------------ terraces

static func _terrace(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1020)
	var fr := M7WorldData.R * 0.86
	b.tag = PLAIN
	# three stepped fields, the back one highest; each has a stone retaining wall on its front edge and crop rows along X
	var zs := [-0.3, 0.0, 0.3]
	var ys := [0.07, 0.038, 0.006]
	for k in 3:
		var z: float = zs[k]
		var y: float = ys[k]
		var half := M7MeshKit._hex_half_width(z, fr) - 0.02
		if half < 0.05:
			continue
		var depth := 0.26
		b.tag = PLAIN
		b.box(Vector3(0, y * 0.5, z), Vector3(half * 2.0, y, depth), SOIL.darkened(0.12), SOIL)
		# the retaining wall: dry stone in two courses on the front face
		b.box(Vector3(0, y * 0.5, z + depth * 0.5 - 0.008), Vector3(half * 2.0, y + 0.01, 0.022), STONE, STONE_LIGHT)
		b.box(Vector3(0, y * 0.3, z + depth * 0.5 + 0.004), Vector3(half * 2.0 - 0.02, y * 0.5, 0.012), STONE_DARK)
		b.tag = CROP
		for r in 4:
			var rz := z - depth * 0.5 + 0.035 + r * 0.06
			b.box(Vector3(0, y + 0.008, rz), Vector3(half * 2.0 - 0.07, 0.016, 0.026), Color("#c9b24a") if (k + r) % 2 == 0 else Color("#8db042"))
	return b.commit()

# ------------------------------------------------------------------ vineyard and olives

static func _vineyard(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1021)
	var fr := M7WorldData.R * 0.84
	b.tag = PLAIN
	b.prism(Vector3(0, 0.0, 0), fr, fr * 0.98, 0.006, 6, SOIL.darkened(0.1), SOIL, PI / 6.0)
	for r in 6:
		var z := -fr * 0.72 + r * (fr * 1.44 / 5.0)
		var half := M7MeshKit._hex_half_width(z, fr) - 0.05
		if half < 0.08:
			continue
		b.tag = PLAIN
		for px in range(-3, 4):
			b.pole(Vector3(px * half / 3.0, 0.004, z), Vector3(px * half / 3.0, 0.07, z), 0.004, WOOD)
		b.pole(Vector3(-half, 0.06, z), Vector3(half, 0.06, z), 0.0022, ROPE)
		b.pole(Vector3(-half, 0.035, z), Vector3(half, 0.035, z), 0.0022, ROPE)
		b.tag = FOLIAGE
		b.box(Vector3(0, 0.048, z), Vector3(half * 2.0, 0.05, 0.036), LEAF if r % 2 == 0 else LEAF_DARK)
		b.tag = PLAIN
		for g in 5:
			b.blob(Vector3(-half * 0.8 + g * half * 0.4, 0.024, z + 0.02 * (1.0 if g % 2 == 0 else -1.0)), Vector3(0.011, 0.015, 0.011), 5, 3, GRAPE, 0.1)
	return b.commit()

static func _olive(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1022)
	b.tag = PLAIN
	# a twisted trunk of two limbs and a wide, low, grey-green crown in two lobes
	b.pole(Vector3(0, 0, 0), Vector3(0.012, 0.045, 0.004), 0.012, TRUNK, 5)
	b.pole(Vector3(0.012, 0.04, 0.004), Vector3(-0.018, 0.075, 0.008), 0.009, TRUNK, 5)
	b.pole(Vector3(0.012, 0.04, 0.004), Vector3(0.036, 0.07, -0.006), 0.008, TRUNK, 5)
	b.tag = FOLIAGE
	b.blob(Vector3(-0.02, 0.092, 0.008), Vector3(0.05, 0.034, 0.045), 7, 4, OLIVE, 0.15, OLIVE_DARK)
	b.blob(Vector3(0.034, 0.084, -0.008), Vector3(0.042, 0.03, 0.04), 7, 4, OLIVE.lightened(0.06), 0.15, OLIVE_DARK)
	return b.commit()

# ------------------------------------------------------------------ press and well

static func _press(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1023)
	b.tag = PLAIN
	var hx := 0.12
	var hz := 0.09
	b.box(Vector3(0, 0.006, 0), Vector3(hx * 2.0 + 0.03, 0.012, hz * 2.0 + 0.03), STONE_DARK, STONE)
	# four posts and a pitched roof, open at the front, a screw beam standing in the middle with its long lever, a vat under it
	for sx in [-hx, hx]:
		for sz in [-hz, hz]:
			b.pole(Vector3(sx, 0.012, sz), Vector3(sx, 0.1, sz), 0.006, WOOD)
	var inner := Vector3(0, 0.08, 0)
	for side in [-1.0, 1.0]:
		var c := ROOF if side > 0.0 else ROOF.darkened(0.12)
		b.quad(Vector3(-hx - 0.012, 0.1, (hz + 0.012) * side), Vector3(hx + 0.012, 0.1, (hz + 0.012) * side), Vector3(hx + 0.012, 0.15, 0), Vector3(-hx - 0.012, 0.15, 0), c, inner)
	for sx in [-1.0, 1.0]:
		b.tri(Vector3(sx * (hx + 0.012), 0.1, hz + 0.012), Vector3(sx * (hx + 0.012), 0.1, -hz - 0.012), Vector3(sx * (hx + 0.012), 0.15, 0), ROOF_DARK, inner)
	b.prism(Vector3(0, 0.012, 0), 0.04, 0.036, 0.03, 8, WOOD_LIGHT, WOOD)
	b.pole(Vector3(0, 0.03, 0), Vector3(0, 0.12, 0), 0.008, STONE)
	b.pole(Vector3(-0.1, 0.1, 0.0), Vector3(0.09, 0.1, 0.02), 0.005, WOOD_LIGHT)
	b.prism(Vector3(0.07, 0.012, 0.06), 0.022, 0.02, 0.04, 7, Color("#7a5a3a"), WOOD_LIGHT)
	for i in 3:
		b.prism(Vector3(-0.08 + 0.03 * i, 0.012, 0.07), 0.013, 0.016, 0.018, 6, BASKET, BASKET.lightened(0.1))
	return b.commit()

static func _well(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1024)
	b.tag = PLAIN
	b.prism(Vector3(0, 0.0, 0), 0.05, 0.046, 0.04, 8, STONE, STONE_LIGHT)
	b.prism(Vector3(0, 0.038, 0), 0.036, 0.036, 0.003, 8, Color("#2a3a46"), Color("#2a3a46"))
	for sx in [-0.05, 0.05]:
		b.pole(Vector3(sx, 0.036, 0), Vector3(sx, 0.13, 0), 0.006, WOOD)
	b.pole(Vector3(-0.056, 0.115, 0), Vector3(0.056, 0.115, 0), 0.006, WOOD_LIGHT)
	b.pole(Vector3(-0.05, 0.13, 0), Vector3(0, 0.15, 0), 0.004, WOOD)
	b.pole(Vector3(0.05, 0.13, 0), Vector3(0, 0.15, 0), 0.004, WOOD)
	b.pole(Vector3(0.056, 0.115, 0), Vector3(0.082, 0.098, 0.0), 0.004, WOOD_LIGHT)    # the crank
	b.pole(Vector3(0, 0.115, 0.0), Vector3(0, 0.07, 0.0), 0.002, ROPE)
	b.prism(Vector3(0, 0.05, -0.008), 0.012, 0.014, 0.02, 6, WOOD, WOOD_LIGHT)
	return b.commit()

# ------------------------------------------------------------------ war galley

static func _galley(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1025)
	b.tag = PLAIN
	M7LooksKit._plank_hull(b, 0.56, 0.075, 0.032)
	# the ram: a shod beak at the waterline at the bow, and a curled stern post
	b.pole(Vector3(0, 0.008, 0.26), Vector3(0, 0.004, 0.34), 0.008, WOOD)
	b.prism(Vector3(0, 0.0045, 0.33), 0.006, 0.0, 0.034, 5, BRONZE, BRONZE)
	b.pole(Vector3(0, 0.034, -0.27), Vector3(0, 0.07, -0.29), 0.006, WOOD_LIGHT)
	# a deck and a bench of rowers' oars: two banks of seven, each a long blade out at an angle
	b.box(Vector3(0, 0.03, 0), Vector3(0.05, 0.004, 0.44), PLANK_UP)
	for k in 7:
		var z := -0.17 + 0.057 * k
		for sx in [-1.0, 1.0]:
			b.pole(Vector3(sx * 0.036, 0.04, z), Vector3(sx * 0.13, -0.006, z - 0.03), 0.0026, WOOD_LIGHT)
			b.box(Vector3(sx * 0.134, -0.006, z - 0.032), Vector3(0.022, 0.003, 0.012), WOOD_LIGHT)
	# shields along both rails in the owner's colour
	b.tag = CIV
	for k in 7:
		var z2 := -0.17 + 0.057 * k
		for sx in [-1.0, 1.0]:
			var save := b.xf
			b.xf = b.xf * Transform3D(Basis(Vector3.UP, PI * 0.5 * sx), Vector3(sx * 0.04, 0.05, z2))
			b.prism(Vector3.ZERO, 0.014, 0.014, 0.003, 7, Color.WHITE, Color.WHITE)
			b.xf = save
	b.tag = PLAIN
	# a single mast with the sail furled on its yard
	b.pole(Vector3(0, 0.03, 0.04), Vector3(0, 0.22, 0.04), 0.0042, WOOD)
	b.pole(Vector3(-0.075, 0.2, 0.04), Vector3(0.075, 0.2, 0.04), 0.004, WOOD)
	b.box(Vector3(0, 0.188, 0.04), Vector3(0.14, 0.018, 0.016), SAIL)
	return b.commit()
