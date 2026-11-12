## Iron Age props: stock-backed displays of barrels (WOD-07), planks (WOD-08) and glass (CER-07), camel caravans (HUS-09), and diplomatic banners.
##
## The goods and banners are laid out by world_builder.gd (`_goods`, `_pacts`) and the camel train by farm_rig.gd. The relay-post mesh remains unplaced until
## the frame can report a built relay. Same units as the rest of the kit (1.0 = one tile
## column, an adult is ~0.1 tall). Land props face +Z; the camel train is a rig mesh and faces +X like the ox cart.
##
##   relay_post       a small stone stable under a tiled roof with a hitching rail, a tethered horse and a pennant in the owner's colour
##   camel_train_a/b  three laden camels in file on a lead rope, legs swapped between the two poses so a walking train reads as walking
##   barrel_stack     hooped barrels stacked in a pyramid with one on its side
##   plank_stack      sawn planks stacked crosswise on bearers, a frame saw leaning on the pile
##   glass_shelf      a rack of blown glass vessels in blue and green on a timber stand
##   vassal_banner    a pole with a long banner in the overlord's colour hung below a chain and gold ring: this town pays tribute
##   league_standard  a pole with a gold ring of linked hoops and a pennant: this town sits in a league
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const LIME := Color("#e2d6bc")
const ROOF := Color("#a8492c")
const ROOF_DARK := Color("#7f3622")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const BARREL := Color("#7a5a3a")
const IRON := Color("#3f444c")
const PLANK := Color("#c9a674")
const PLANK_DARK := Color("#a98a58")
const GOLD := Color("#c9973c")
const ROPE := Color("#b9a68a")
const CAMEL := Color("#b8935a")
const CAMEL_DARK := Color("#8a6a3e")
const HORSE := Color("#7a4a2a")
const GLASS_BLUE := Color("#4a86a8")
const GLASS_GREEN := Color("#5aa078")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV

static func build(kit) -> void:
	kit.meshes["relay_post"] = _relay(kit)
	kit.meshes["camel_train_a"] = _train(kit, 0)
	kit.meshes["camel_train_b"] = _train(kit, 1)
	kit.meshes["barrel_stack"] = _barrels(kit)
	kit.meshes["plank_stack"] = _planks(kit)
	kit.meshes["glass_shelf"] = _glass(kit)
	kit.meshes["vassal_banner"] = _vassal(kit)
	kit.meshes["league_standard"] = _league(kit)

# ------------------------------------------------------------------ relay post

static func _relay(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1030)
	b.tag = PLAIN
	b.box(Vector3(0, 0.01, 0), Vector3(0.2, 0.02, 0.15), STONE_DARK, STONE)
	b.box(Vector3(0, 0.05, -0.02), Vector3(0.16, 0.06, 0.1), LIME, LIME)
	var inner := Vector3(0, 0.05, 0)
	for side in [-1.0, 1.0]:
		var c := ROOF if side > 0.0 else ROOF.darkened(0.12)
		b.quad(Vector3(-0.095, 0.078, -0.02 + 0.062 * side), Vector3(0.095, 0.078, -0.02 + 0.062 * side), Vector3(0.095, 0.115, -0.02), Vector3(-0.095, 0.115, -0.02), c, inner)
	for sx in [-1.0, 1.0]:
		b.tri(Vector3(sx * 0.095, 0.078, 0.042), Vector3(sx * 0.095, 0.078, -0.082), Vector3(sx * 0.095, 0.115, -0.02), ROOF_DARK, inner)
	b.box(Vector3(0, 0.04, 0.032), Vector3(0.036, 0.044, 0.004), Color("#2a2119"))
	# a hitching rail out front, with a tethered horse beside it
	for sx in [-0.07, 0.07]:
		b.pole(Vector3(sx, 0.02, 0.1), Vector3(sx, 0.06, 0.1), 0.004, WOOD)
	b.pole(Vector3(-0.07, 0.055, 0.1), Vector3(0.07, 0.055, 0.1), 0.004, WOOD_LIGHT)
	b.blob(Vector3(0.04, 0.05, 0.13), Vector3(0.012, 0.016, 0.034), 6, 3, HORSE, 0.04)
	b.pole(Vector3(0.04, 0.056, 0.15), Vector3(0.04, 0.078, 0.17), 0.005, HORSE)
	b.blob(Vector3(0.04, 0.082, 0.18), Vector3(0.008, 0.009, 0.014), 5, 3, HORSE.lightened(0.05))
	for lx in [0.033, 0.047]:
		for lz in [0.118, 0.145]:
			b.pole(Vector3(lx, 0.04, lz), Vector3(lx, 0.0, lz), 0.0026, Color("#2f241b"))
	# the owner's pennant on a pole at the corner
	b.pole(Vector3(0.1, 0.02, -0.07), Vector3(0.1, 0.19, -0.07), 0.003, WOOD)
	b.tag = CIV
	var top := Vector3(0.1, 0.185, -0.07)
	b.quad(top, top + Vector3(0.07, -0.01, 0), top + Vector3(0.07, -0.035, 0), top + Vector3(0, -0.045, 0), Color.WHITE, top + Vector3(0, -0.03, 1.0))
	b.tag = PLAIN
	return b.commit()

# ------------------------------------------------------------------ camel caravan (faces +X)

static func _camel(b: M7MeshBuilder, p: Vector3, phase: int, coat: Color) -> void:
	var legc := coat.darkened(0.35)
	var s := 0.007 if phase == 0 else -0.007
	for lx in [-0.032, 0.032]:
		for lz in [-0.012, 0.012]:
			var sw: float = s * (1.0 if (lx > 0.0) == (lz > 0.0) else -1.0)
			b.pole(p + Vector3(lx + sw, 0.056, lz), p + Vector3(lx - sw, 0.0, lz), 0.0048, legc)
	b.blob(p + Vector3(0, 0.07, 0), Vector3(0.05, 0.022, 0.02), 7, 4, coat, 0.05)
	b.blob(p + Vector3(-0.008, 0.098, 0), Vector3(0.016, 0.02, 0.016), 6, 3, coat.darkened(0.05), 0.06)       # the hump
	b.pole(p + Vector3(0.036, 0.078, 0), p + Vector3(0.062, 0.118, 0), 0.0075, coat)                          # the long neck
	b.blob(p + Vector3(0.072, 0.122, 0), Vector3(0.017, 0.01, 0.009), 6, 3, coat.lightened(0.05), 0.0)
	b.pole(p + Vector3(-0.046, 0.074, 0), p + Vector3(-0.056, 0.05, 0), 0.0022, legc)

static func _train(kit, phase: int) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1031 + phase)
	b.tag = PLAIN
	for k in 3:
		var x := 0.0 - 0.16 * k
		_camel(b, Vector3(x, 0, 0), phase if k % 2 == 0 else 1 - phase, CAMEL if k != 1 else CAMEL_DARK)
		# a load slung across the back: two bales and a lashed barrel
		b.box(Vector3(x - 0.006, 0.104, -0.018), Vector3(0.028, 0.024, 0.016), Color("#a5814f"))
		b.box(Vector3(x - 0.006, 0.104, 0.018), Vector3(0.028, 0.024, 0.016), Color("#bf8c58"))
		b.prism(Vector3(x - 0.006, 0.112, 0), 0.011, 0.011, 0.02, 6, BARREL, WOOD_LIGHT)
		if k > 0:
			b.pole(Vector3(x + 0.16 - 0.056, 0.05, 0), Vector3(x + 0.062, 0.1, 0), 0.0016, ROPE)
	# the drover walking at the head, a staff in his hand
	var hx := 0.14
	b.tag = PLAIN
	b.prism(Vector3(hx - 0.008, 0, 0.0), 0.0065, 0.007, 0.036, 5, Color("#4a3a2a"), Color("#4a3a2a"))
	b.prism(Vector3(hx + 0.008, 0, 0.0), 0.0065, 0.007, 0.036, 5, Color("#4a3a2a"), Color("#4a3a2a"))
	b.prism(Vector3(hx, 0.034, 0), 0.018, 0.015, 0.036, 7, Color("#8a6a48"), Color("#8a6a48"))
	b.blob(Vector3(hx, 0.084, 0), Vector3(0.0125, 0.013, 0.0125), 7, 4, M7MeshKit.SKIN, 0.0)
	b.pole(Vector3(hx + 0.03, 0.0, 0.02), Vector3(hx + 0.03, 0.09, 0.02), 0.0026, WOOD_LIGHT)
	return b.commit()

# ------------------------------------------------------------------ trade goods

static func _barrels(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1033)
	b.tag = PLAIN
	for row in 3:
		for i in 3 - row:
			var x := (i - (2 - row) * 0.5) * 0.054
			var y := 0.0 + row * 0.046
			b.prism(Vector3(x, y, 0), 0.026, 0.026, 0.046, 8, BARREL, WOOD_LIGHT)
			b.prism(Vector3(x, y + 0.012, 0), 0.0275, 0.0275, 0.004, 8, IRON, IRON)
			b.prism(Vector3(x, y + 0.03, 0), 0.0275, 0.0275, 0.004, 8, IRON, IRON)
	# one on its side in front
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis(Vector3.BACK, PI * 0.5), Vector3(0.07, 0.026, 0.07))
	b.prism(Vector3(0, -0.023, 0), 0.026, 0.026, 0.046, 8, BARREL, WOOD_LIGHT)
	b.prism(Vector3(0, -0.012, 0), 0.0275, 0.0275, 0.004, 8, IRON, IRON)
	b.xf = save
	return b.commit()

static func _planks(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1034)
	b.tag = PLAIN
	for bx in [-0.07, 0.07]:
		b.box(Vector3(bx, 0.01, 0), Vector3(0.014, 0.02, 0.1), WOOD)
	for layer in 6:
		var cross := layer % 2 == 1
		for k in 4:
			var o := (k - 1.5) * 0.026
			var col := PLANK if (layer + k) % 2 == 0 else PLANK_DARK
			if cross:
				b.box(Vector3(o, 0.026 + layer * 0.011, 0), Vector3(0.024, 0.01, 0.12), col)
			else:
				b.box(Vector3(0, 0.026 + layer * 0.011, o), Vector3(0.18, 0.01, 0.024), col)
	# a frame saw leaning against the pile: two uprights, a blade between them
	b.pole(Vector3(0.1, 0.0, 0.07), Vector3(0.08, 0.12, 0.06), 0.003, WOOD_LIGHT)
	b.pole(Vector3(0.12, 0.0, 0.07), Vector3(0.1, 0.12, 0.06), 0.003, WOOD_LIGHT)
	b.box(Vector3(0.09, 0.07, 0.065), Vector3(0.002, 0.1, 0.01), IRON)
	return b.commit()

static func _glass(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1035)
	b.tag = PLAIN
	for sx in [-0.07, 0.07]:
		b.pole(Vector3(sx, 0.0, 0), Vector3(sx, 0.11, 0), 0.005, WOOD)
	for y in [0.03, 0.07]:
		b.box(Vector3(0, y, 0), Vector3(0.16, 0.005, 0.04), WOOD_LIGHT)
		for k in 4:
			var x := -0.055 + 0.037 * k
			var col := GLASS_BLUE if (k + int(y * 100)) % 2 == 0 else GLASS_GREEN
			b.blob(Vector3(x, y + 0.016, 0), Vector3(0.011, 0.014, 0.011), 6, 3, col, 0.0, col.lightened(0.2))
			b.pole(Vector3(x, y + 0.026, 0), Vector3(x, y + 0.036, 0), 0.0035, col)
	return b.commit()

# ------------------------------------------------------------------ pacts

static func _vassal(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1036)
	b.tag = PLAIN
	b.prism(Vector3(0, 0.0, 0), 0.034, 0.03, 0.022, 6, STONE, STONE_LIGHT)
	b.pole(Vector3(0, 0.022, 0), Vector3(0, 0.34, 0), 0.0034, WOOD)
	b.blob(Vector3(0, 0.345, 0), Vector3(0.008, 0.008, 0.008), 5, 3, GOLD, 0.0)
	# a chain of three links hangs from a gold ring, the overlord's long banner under it
	b.prism(Vector3(0, 0.3, 0), 0.016, 0.016, 0.004, 8, GOLD, GOLD)
	for k in 3:
		b.prism(Vector3(0.012, 0.28 - 0.016 * k, 0), 0.005, 0.005, 0.01, 5, IRON, IRON)
	b.tag = CIV
	var top := Vector3(0, 0.23, 0)
	b.quad(top, top + Vector3(0, -0.01, 0.1), top + Vector3(0, -0.12, 0.09), top + Vector3(0, -0.13, 0), Color.WHITE, top + Vector3(1.0, -0.06, 0))
	b.tri(top + Vector3(0, -0.13, 0), top + Vector3(0, -0.12, 0.09), top + Vector3(0, -0.16, 0.045), Color.WHITE, top + Vector3(1.0, -0.1, 0))
	b.tag = PLAIN
	return b.commit()

static func _league(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1037)
	b.tag = PLAIN
	b.prism(Vector3(0, 0.0, 0), 0.034, 0.03, 0.022, 6, STONE, STONE_LIGHT)
	b.pole(Vector3(0, 0.022, 0), Vector3(0, 0.36, 0), 0.0034, WOOD)
	# three linked gold hoops, a gold finial and a pennant in the owner's colour
	for k in 3:
		var a := TAU * k / 3.0
		var c := Vector3(cos(a) * 0.016, 0.3, sin(a) * 0.016)
		for i in 10:
			var a0 := TAU * i / 10.0
			var a1 := TAU * (i + 1) / 10.0
			var save := b.xf
			b.xf = b.xf * Transform3D(Basis(Vector3.UP, -a), c)
			b.pole(Vector3(0, sin(a0) * 0.014, cos(a0) * 0.014), Vector3(0, sin(a1) * 0.014, cos(a1) * 0.014), 0.0016, GOLD)
			b.xf = save
	b.blob(Vector3(0, 0.37, 0), Vector3(0.008, 0.01, 0.008), 5, 3, GOLD, 0.0)
	b.tag = CIV
	var top := Vector3(0, 0.26, 0)
	b.quad(top, top + Vector3(0, -0.01, 0.09), top + Vector3(0, -0.045, 0.075), top + Vector3(0, -0.055, 0), Color.WHITE, top + Vector3(1.0, -0.03, 0))
	b.tag = PLAIN
	return b.commit()
