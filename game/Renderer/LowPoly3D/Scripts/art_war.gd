## Iron Age metalwork and war: the forge yard (MET-09 Iron Tools), the counterweighted siege engine (WAR-09) and what it leaves of a wall.
##
## Laid out by world_builder.gd (`_forge_yard`, `_siege`, `_stone_walls`) and animated by war_rig.gd. Same units as the rest of the kit (1.0 = one
## tile column, an adult is ~0.1 tall); fronts face +Z.
##
##   forge_yard / forge_yard_hot   a bloomery stack with an anvil, a charcoal pile, an ore heap and a tool rack; the hot one has the furnace mouth
##                                 and a bar on the anvil glowing (the rig adds flame, light and smoke above the chimney)
##   siege_base                    the timber frame and windlass of a counterweighted engine, firing toward +Z
##   siege_arm                     its throwing arm, pivot at the origin (the rig swings it about X); counterweight on the short end
##   siege_stone                   the shot
##   wall_ruin / wall_rubble       a wall run knocked half down, and a heap where it stood (SEG long, like wall_seg)
extends RefCounted

const CLAY := Color("#9a6a4a")
const CLAY_DARK := Color("#7a4f35")
const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const SOOT := Color("#2a2119")
const IRON := Color("#3f444c")
const IRON_LIGHT := Color("#6b717a")
const CHARCOAL := Color("#2f2b28")
const ORE := Color("#8a4a32")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const ROPE := Color("#b9a68a")
const WATER := Color("#4a7a96")
const GLOW := Color("#ff6a1c")
const GLOW_CORE := Color("#ffc060")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE

static func build(kit) -> void:
	kit.meshes["forge_yard"] = _yard(kit, false)
	kit.meshes["forge_yard_hot"] = _yard(kit, true)
	kit.meshes["siege_base"] = _siege_base(kit)
	kit.meshes["siege_arm"] = _siege_arm(kit)
	kit.meshes["siege_stone"] = _stone(kit)
	kit.meshes["wall_ruin"] = _ruin(kit)
	kit.meshes["wall_rubble"] = _rubble(kit)

# ------------------------------------------------------------------ forge yard

static func _yard(kit, hot: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(950 + (1 if hot else 0))
	b.tag = PLAIN
	# trodden ground
	b.prism(Vector3(0, 0.0, 0), 0.22, 0.21, 0.006, 8, Color("#5a4a3a"), Color("#4a3c2e"), 0.2)
	# the bloomery: a clay-and-stone stack with a firing mouth at the front and a chimney
	b.prism(Vector3(0, 0.0, -0.05), 0.075, 0.062, 0.05, 7, STONE_DARK, STONE)
	b.prism(Vector3(0, 0.05, -0.05), 0.062, 0.04, 0.1, 7, CLAY, CLAY_DARK)
	b.prism(Vector3(0, 0.15, -0.05), 0.04, 0.034, 0.03, 7, STONE, STONE_LIGHT)
	b.box(Vector3(0, 0.04, -0.005), Vector3(0.05, 0.046, 0.02), SOOT)
	if hot:
		b.tag = EMISSIVE
		b.box(Vector3(0, 0.036, 0.006), Vector3(0.036, 0.028, 0.006), GLOW)
		b.box(Vector3(0, 0.034, 0.008), Vector3(0.02, 0.014, 0.004), GLOW_CORE)
		b.prism(Vector3(0, 0.178, -0.05), 0.02, 0.02, 0.004, 7, GLOW, GLOW)
		b.tag = PLAIN
	# bellows against the stack, a leather bag on a frame
	b.prism(Vector3(0.09, 0.0, -0.045), 0.024, 0.016, 0.026, 6, Color("#7a5a3a"), Color("#8a6a48"))
	b.pole(Vector3(0.075, 0.018, -0.045), Vector3(0.04, 0.03, -0.047), 0.004, WOOD)
	# anvil on a stump, a quench trough and the tool rack in front
	b.prism(Vector3(-0.09, 0.0, 0.07), 0.026, 0.024, 0.045, 6, WOOD, WOOD_LIGHT)
	b.box(Vector3(-0.09, 0.056, 0.07), Vector3(0.05, 0.024, 0.026), IRON, IRON_LIGHT)
	b.box(Vector3(-0.123, 0.056, 0.07), Vector3(0.022, 0.012, 0.014), IRON)
	if hot:
		b.tag = EMISSIVE
		b.box(Vector3(-0.09, 0.07, 0.07), Vector3(0.034, 0.006, 0.01), GLOW)
		b.tag = PLAIN
	b.box(Vector3(0.1, 0.014, 0.08), Vector3(0.06, 0.028, 0.032), WOOD)
	b.box(Vector3(0.1, 0.027, 0.08), Vector3(0.05, 0.004, 0.022), WATER)
	for sx in [-0.03, 0.03]:
		b.pole(Vector3(0.0 + sx, 0.0, 0.13), Vector3(0.0 + sx, 0.07, 0.13), 0.004, WOOD)
	b.pole(Vector3(-0.034, 0.066, 0.13), Vector3(0.034, 0.066, 0.13), 0.003, WOOD_LIGHT)
	for k in 3:
		var x := -0.02 + 0.02 * k
		b.pole(Vector3(x, 0.066, 0.13), Vector3(x, 0.04, 0.134), 0.0024, IRON)
		b.box(Vector3(x, 0.036, 0.134), Vector3(0.012, 0.008, 0.004), IRON_LIGHT)
	# the charcoal pile on the left, the ore heap on the right behind
	for i in 5:
		b.blob(Vector3(-0.15 + 0.022 * (i % 3), 0.014 + 0.012 * (i / 3), -0.06 + 0.02 * (i % 2)), Vector3(0.03, 0.02, 0.026), 5, 3, CHARCOAL, 0.15)
	for i in 4:
		b.blob(Vector3(0.14 + 0.02 * (i % 2), 0.012 + 0.01 * (i / 2), -0.1 + 0.02 * (i % 3)), Vector3(0.026, 0.018, 0.022), 5, 3, ORE, 0.18, ORE.darkened(0.2))
	return b.commit()

# ------------------------------------------------------------------ siege engine

static func _siege_base(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(960)
	b.tag = PLAIN
	var pivot_y := 0.2
	# two long ground beams with cross beams, A-frames up to the axle
	for sx in [-0.045, 0.045]:
		b.box(Vector3(sx, 0.01, 0.0), Vector3(0.016, 0.02, 0.36), WOOD)
		b.pole(Vector3(sx, 0.012, -0.1), Vector3(sx * 0.4, pivot_y, 0.0), 0.007, WOOD_LIGHT)
		b.pole(Vector3(sx, 0.012, 0.1), Vector3(sx * 0.4, pivot_y, 0.0), 0.007, WOOD_LIGHT)
	for z in [-0.14, 0.0, 0.14]:
		b.box(Vector3(0, 0.01, z), Vector3(0.11, 0.014, 0.016), WOOD)
	b.pole(Vector3(-0.025, pivot_y, 0.0), Vector3(0.025, pivot_y, 0.0), 0.009, IRON)
	# a windlass at the back to haul the long arm down: drum, spokes, rope
	b.pole(Vector3(-0.05, 0.05, -0.15), Vector3(0.05, 0.05, -0.15), 0.01, WOOD_LIGHT)
	for a in [0.0, PI * 0.5, PI, PI * 1.5]:
		b.pole(Vector3(0.05, 0.05, -0.15), Vector3(0.05, 0.05 + cos(a) * 0.03, -0.15 + sin(a) * 0.03), 0.003, WOOD)
	b.pole(Vector3(0.0, 0.05, -0.15), Vector3(0.0, 0.03, 0.12), 0.002, ROPE)
	# wheels at the back
	for sx in [-0.058, 0.058]:
		b.prism(Vector3(sx, 0.026, -0.13), 0.024, 0.024, 0.01, 8, WOOD, WOOD_LIGHT, 0.0)
	return b.commit()

## Pivot at the origin; the long arm runs along +Z (the throwing end, sling hanging), the short arm along -Z with the counterweight box. At rest the long
## end points up and out; the rig tips it about X.
static func _siege_arm(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(961)
	b.tag = PLAIN
	b.pole(Vector3(0, 0, -0.1), Vector3(0, 0, 0.34), 0.007, WOOD_LIGHT)
	b.box(Vector3(0, -0.035, -0.1), Vector3(0.07, 0.07, 0.07), STONE_DARK, STONE)
	b.box(Vector3(0, -0.035, -0.1), Vector3(0.074, 0.01, 0.074), IRON)
	b.pole(Vector3(0, 0, 0.34), Vector3(0, -0.07, 0.36), 0.002, ROPE)
	b.blob(Vector3(0, -0.08, 0.365), Vector3(0.012, 0.01, 0.012), 5, 3, Color("#7a5a3a"), 0.1)
	return b.commit()

static func _stone(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(962)
	b.tag = PLAIN
	b.blob(Vector3.ZERO, Vector3(0.015, 0.014, 0.015), 6, 3, STONE_DARK, 0.1, STONE)
	return b.commit()

# ------------------------------------------------------------------ what a siege leaves of a wall

const SEG := 0.16

static func _ruin(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(970)
	b.tag = PLAIN
	b.box(Vector3(0, 0.006, 0), Vector3(SEG, 0.018, 0.044), STONE_DARK)
	# a broken stub of wall: stepped down across the run, the stone showing pale where it broke
	b.box(Vector3(-0.055, 0.026, 0), Vector3(0.05, 0.05, 0.036), STONE, STONE_LIGHT)
	b.box(Vector3(0.0, 0.016, 0), Vector3(0.04, 0.03, 0.036), STONE, STONE_LIGHT)
	b.box(Vector3(0.05, 0.01, 0), Vector3(0.05, 0.02, 0.036), STONE, STONE_LIGHT)
	for i in 4:
		b.blob(Vector3(-0.06 + 0.04 * i, 0.012, 0.03 + 0.01 * (i % 2)), Vector3(0.02, 0.012, 0.016), 5, 3, STONE_LIGHT, 0.2, STONE)
	return b.commit()

static func _rubble(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(971)
	b.tag = PLAIN
	for i in 8:
		var x := -0.07 + 0.02 * i
		b.blob(Vector3(x, 0.01 + 0.012 * sin(i * 1.3), 0.012 * sin(i * 2.1)), Vector3(0.026, 0.018 + 0.008 * (i % 3), 0.024), 5, 3,
			STONE_LIGHT if i % 2 == 0 else STONE, 0.22, STONE_DARK)
	return b.commit()
