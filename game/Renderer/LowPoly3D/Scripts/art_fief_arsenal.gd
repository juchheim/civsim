## Faith & Fief (E5) Arsenal (WAR-12), docs/MVP/37 and docs/MVP/32 §4.13. It replaces the powder mill, which is retired (art_fief_power.gd keeps its meshes for old frames).
##
## Not wired to the simulation yet (docs/MVP/32 §9: the FiefFrame comes with the E5 mode plumbing); photographed in the Fief gallery
## (LowPoly3DView.show_fief_gallery). Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.1 tall). One per civilization, an outer work
## (docs/MVP/31a) set outside the walls, roughly 0.66 by 0.54. It faces +Z (the gate toward the village); the owner's colour is the banner (TAG_CIV).
##
##   arsenal        the working yard: a low stone wall round it and a gate with a sign (crossed barrels over a flame) and the owner's banner; a stone foundry hall with a
##                  glowing casting arch and a tall chimney; an open timber boring shed with a cannon barrel on its frame; a powder magazine behind earth banks with a
##                  red danger flag and a stack of kegs; a finished cannon with its shot, a counterweight siege engine cocked, and a jib crane lifting a casting
##   arsenal_idle   the same with the furnace dark and the chimney cold, the banner lowered, fewer kegs, the engine at rest and the crane hook empty (no iron, no powder,
##                  or no crew in post)
##   arsenal_site   the works under construction (docs/MVP/32 §4.13: a hundred work-seasons): the yard wall as stubs, the foundry's footing with scaffolding round it, stacks
##                  of dressed blocks and timber, a treadwheel crane, the magazine's banks already piled
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const BRICK := Color("#9a4a34")
const BRICK_DARK := Color("#74382a")
const ROOF := Color("#a8492c")
const ROOF_DARK := Color("#7f3622")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const PLANK := Color("#b8956a")
const EARTH := Color("#7b6247")
const GRAVEL := Color("#9a8a6e")
const GRASS := Color("#7a8a44")
const SAND := Color("#d8c08e")
const IRON := Color("#4a4f57")
const IRON_LIGHT := Color("#7a8089")
const ARCH := Color("#2a2119")
const GLOW := Color("#ff9a3c")
const GOLD := Color("#c9973c")
const BLACK := Color("#2a2119")
const KEG := Color("#4a3a2a")
const KEG_LIGHT := Color("#5e4a36")
const DANGER := Color("#b8352c")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE
const CIV := M7MeshBuilder.TAG_CIV

const WALL_H := 0.045
const WALL_T := 0.016
const FLOOR := 0.006

static func build(kit) -> void:
	kit.meshes["arsenal"] = _arsenal(kit, true)
	kit.meshes["arsenal_idle"] = _arsenal(kit, false)
	kit.meshes["arsenal_site"] = _site(kit)

## The ground and the yard wall with its gate: the wall runs round three sides and the front, open at the gate between the two piers.
static func _yard(b: M7MeshBuilder, complete: bool) -> void:
	b.tag = PLAIN
	b.box(Vector3(0, 0.003, 0.0), Vector3(0.66, 0.006, 0.54), EARTH)
	b.box(Vector3(0, 0.0065, 0.02), Vector3(0.56, 0.002, 0.44), GRAVEL)
	var h := WALL_H if complete else WALL_H * 0.4
	var y := FLOOR + h * 0.5
	b.box(Vector3(0, y, -0.25), Vector3(0.62, h, WALL_T), STONE, STONE_LIGHT)
	b.box(Vector3(-0.31, y, 0.0), Vector3(WALL_T, h, 0.5), STONE, STONE_LIGHT)
	b.box(Vector3(0.31, y, 0.0), Vector3(WALL_T, h, 0.5), STONE, STONE_LIGHT)
	b.box(Vector3(-0.185, y, 0.25), Vector3(0.25, h, WALL_T), STONE, STONE_LIGHT)
	b.box(Vector3(0.185, y, 0.25), Vector3(0.25, h, WALL_T), STONE, STONE_LIGHT)
	if complete:
		# merlons along the back wall
		for k in 9:
			b.box(Vector3(-0.28 + 0.07 * k, FLOOR + WALL_H + 0.006, -0.25), Vector3(0.03, 0.012, WALL_T), STONE_LIGHT)

## A wheel with its rim in the Y-Z plane (axis along X): an octagon of rim poles and two crossed spokes.
static func _wheel(b: M7MeshBuilder, c: Vector3, r: float) -> void:
	for i in 8:
		var a0 := TAU * i / 8.0
		var a1 := TAU * (i + 1) / 8.0
		b.pole(c + Vector3(0, sin(a0) * r, cos(a0) * r), c + Vector3(0, sin(a1) * r, cos(a1) * r), 0.0028, WOOD_LIGHT)
	for a in [0.0, PI * 0.5]:
		b.pole(c + Vector3(0, sin(a) * r, cos(a) * r), c - Vector3(0, sin(a) * r, cos(a) * r), 0.002, WOOD)

## A keg: an iron-hooped barrel standing at p.
static func _keg(b: M7MeshBuilder, p: Vector3, r: float = 0.011) -> void:
	b.prism(p, r, r, 0.022, 8, KEG, KEG_LIGHT)
	b.prism(p + Vector3(0, 0.005, 0), r * 1.07, r * 1.07, 0.0022, 8, IRON, IRON)
	b.prism(p + Vector3(0, 0.015, 0), r * 1.07, r * 1.07, 0.0022, 8, IRON, IRON)

static func _arsenal(kit, working: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1750 + (0 if working else 1))
	_yard(b, true)
	b.tag = PLAIN
	_gate(b, working)
	_foundry(b, working)
	_boring_shed(b)
	_magazine(b, working)
	_yard_works(b, working)
	return b.commit()

## The gate: two stone piers, a lintel with the sign, doors swung open, a banner pole on the right pier.
static func _gate(b: M7MeshBuilder, working: bool) -> void:
	for sx in [-1.0, 1.0]:
		b.prism(Vector3(sx * 0.06, FLOOR, 0.25), 0.02, 0.018, 0.075, 4, STONE, STONE_LIGHT, PI / 4.0)
	b.box(Vector3(0, 0.083, 0.25), Vector3(0.14, 0.014, 0.03), STONE_LIGHT)
	# the sign on the lintel: a dark plate with two gold barrels crossed over a flame
	b.box(Vector3(0, 0.083, 0.2655), Vector3(0.05, 0.02, 0.003), BLACK)
	b.pole(Vector3(-0.018, 0.076, 0.2685), Vector3(0.018, 0.09, 0.2685), 0.0026, GOLD)
	b.pole(Vector3(-0.018, 0.09, 0.2685), Vector3(0.018, 0.076, 0.2685), 0.0026, GOLD)
	b.tag = EMISSIVE
	b.tri(Vector3(-0.005, 0.0905, 0.2695), Vector3(0.005, 0.0905, 0.2695), Vector3(0.0, 0.1, 0.2695), GLOW if working else Color("#8a5a2a"), Vector3(0, 0.09, 1.0))
	b.tag = PLAIN
	# the doors, swung open outward
	for sx in [-1.0, 1.0]:
		b.quad(Vector3(sx * 0.058, FLOOR + 0.002, 0.25), Vector3(sx * 0.058, 0.074, 0.25), Vector3(sx * 0.09, 0.074, 0.285), Vector3(sx * 0.09, FLOOR + 0.002, 0.285), WOOD_LIGHT, Vector3(sx * 0.07, 0.04, 0.27))
		b.quad(Vector3(sx * 0.058, FLOOR + 0.002, 0.25), Vector3(sx * 0.09, FLOOR + 0.002, 0.285), Vector3(sx * 0.09, 0.074, 0.285), Vector3(sx * 0.058, 0.074, 0.25), WOOD, Vector3(sx * 0.07, 0.04, 0.2))
		b.pole(Vector3(sx * 0.06, 0.03, 0.2505), Vector3(sx * 0.09, 0.03, 0.2845), 0.0016, IRON)
	# the flag pole and the owner's banner (raised while the Arsenal works, lowered when it idles)
	b.pole(Vector3(0.075, FLOOR, 0.25), Vector3(0.075, 0.2, 0.25), 0.0028, WOOD)
	b.tag = CIV
	if working:
		b.quad(Vector3(0.075, 0.2, 0.25), Vector3(0.15, 0.195, 0.25), Vector3(0.15, 0.155, 0.25), Vector3(0.075, 0.16, 0.25), Color.WHITE, Vector3(0.11, 0.18, 1.0))
		b.quad(Vector3(0.075, 0.2, 0.25), Vector3(0.15, 0.195, 0.25), Vector3(0.15, 0.155, 0.25), Vector3(0.075, 0.16, 0.25), Color.WHITE, Vector3(0.11, 0.18, -1.0))
	else:
		b.quad(Vector3(0.075, 0.2, 0.25), Vector3(0.1, 0.2, 0.25), Vector3(0.1, 0.14, 0.25), Vector3(0.075, 0.14, 0.25), Color.WHITE, Vector3(0.09, 0.17, 1.0))
		b.quad(Vector3(0.075, 0.2, 0.25), Vector3(0.1, 0.2, 0.25), Vector3(0.1, 0.14, 0.25), Vector3(0.075, 0.14, 0.25), Color.WHITE, Vector3(0.09, 0.17, -1.0))
	b.tag = PLAIN

## The foundry: a long stone hall with a gabled tile roof, a casting arch glowing while it works, a tall brick chimney and a sand bed of moulds in front.
static func _foundry(b: M7MeshBuilder, working: bool) -> void:
	b.box(Vector3(-0.1, FLOOR + 0.0375, -0.14), Vector3(0.3, 0.075, 0.14), STONE_LIGHT, STONE_LIGHT)
	# roof: front slope, back slope and the two gable ends
	b.quad(Vector3(-0.26, 0.081, -0.065), Vector3(0.06, 0.081, -0.065), Vector3(0.06, 0.128, -0.14), Vector3(-0.26, 0.128, -0.14), ROOF, Vector3(-0.1, 0.1, -0.1))
	b.quad(Vector3(-0.26, 0.128, -0.14), Vector3(0.06, 0.128, -0.14), Vector3(0.06, 0.081, -0.215), Vector3(-0.26, 0.081, -0.215), ROOF_DARK, Vector3(-0.1, 0.1, -0.18))
	b.tri(Vector3(-0.26, 0.081, -0.065), Vector3(-0.26, 0.081, -0.215), Vector3(-0.26, 0.128, -0.14), STONE, Vector3(-1.0, 0.1, -0.14))
	b.tri(Vector3(0.06, 0.081, -0.065), Vector3(0.06, 0.128, -0.14), Vector3(0.06, 0.081, -0.215), STONE, Vector3(1.0, 0.1, -0.14))
	# the casting arch (left) and the people's door (right) on the front face
	b.box(Vector3(-0.155, 0.04, -0.0685), Vector3(0.05, 0.06, 0.004), ARCH)
	b.box(Vector3(-0.155, 0.072, -0.0685), Vector3(0.056, 0.008, 0.005), STONE_DARK)
	if working:
		b.tag = EMISSIVE
		b.box(Vector3(-0.155, 0.03, -0.0665), Vector3(0.034, 0.034, 0.003), GLOW)
		b.tag = PLAIN
	b.box(Vector3(-0.025, 0.032, -0.0685), Vector3(0.026, 0.05, 0.004), WOOD)
	for k in 3:
		b.box(Vector3(-0.09 + 0.03 * k, 0.05, -0.0685), Vector3(0.012, 0.022, 0.003), ARCH)
	# the chimney: a tapering brick stack off the back, with a crown; a glowing mouth while the furnace burns
	b.prism(Vector3(-0.2, 0.08, -0.185), 0.024, 0.015, 0.1, 6, BRICK, BRICK_DARK)
	b.prism(Vector3(-0.2, 0.18, -0.185), 0.019, 0.019, 0.007, 6, STONE_DARK, STONE_DARK)
	if working:
		b.tag = EMISSIVE
		b.prism(Vector3(-0.2, 0.187, -0.185), 0.012, 0.012, 0.002, 6, GLOW, GLOW)
		b.tag = PLAIN
	# the sand bed of moulds with cast barrels cooling in it
	b.box(Vector3(-0.155, 0.008, -0.028), Vector3(0.11, 0.003, 0.07), SAND)
	b.pole(Vector3(-0.2, 0.0135, -0.04), Vector3(-0.115, 0.0135, -0.04), 0.0062, IRON, 8)
	b.pole(Vector3(-0.2, 0.0135, -0.018), Vector3(-0.115, 0.0135, -0.018), 0.0062, IRON_LIGHT if working else IRON, 8)
	b.prism(Vector3(-0.115, 0.0095, -0.04), 0.0095, 0.0095, 0.0045, 8, IRON, IRON)

## The boring shed: four posts, a plank roof, and a cannon barrel lying on a boring frame, a bar and a horse-wheel at its end.
static func _boring_shed(b: M7MeshBuilder) -> void:
	for sx in [0.075, 0.265]:
		b.pole(Vector3(sx, FLOOR, -0.205), Vector3(sx, 0.1, -0.205), 0.004, WOOD)
		b.pole(Vector3(sx, FLOOR, -0.135), Vector3(sx, 0.088, -0.135), 0.004, WOOD)
	# a lean-to over the back half only, so the barrel on its frame shows from the gate: the top slope faces up, the underside faces down
	b.quad(Vector3(0.06, 0.088, -0.135), Vector3(0.28, 0.088, -0.135), Vector3(0.28, 0.104, -0.22), Vector3(0.06, 0.104, -0.22), PLANK, Vector3(0.17, 0.05, -0.18))
	b.quad(Vector3(0.06, 0.085, -0.135), Vector3(0.06, 0.101, -0.22), Vector3(0.28, 0.101, -0.22), Vector3(0.28, 0.085, -0.135), WOOD, Vector3(0.17, 0.15, -0.18))
	# the frame and the barrel on it
	b.box(Vector3(0.17, 0.02, -0.145), Vector3(0.19, 0.01, 0.03), WOOD)
	for sx in [0.11, 0.23]:
		b.box(Vector3(sx, 0.03, -0.145), Vector3(0.012, 0.03, 0.036), WOOD_LIGHT)
	b.pole(Vector3(0.09, 0.042, -0.145), Vector3(0.255, 0.042, -0.145), 0.0125, IRON, 8)
	b.pole(Vector3(0.255, 0.042, -0.145), Vector3(0.29, 0.042, -0.145), 0.004, WOOD)
	b.pole(Vector3(0.29, 0.042, -0.18), Vector3(0.29, 0.042, -0.115), 0.003, WOOD_LIGHT)

## The magazine: a small stone vault with an iron door, banked with earth on three sides and set apart in the corner, a red flag and kegs.
static func _magazine(b: M7MeshBuilder, working: bool) -> void:
	b.box(Vector3(0.2, 0.03, 0.1), Vector3(0.09, 0.048, 0.07), STONE_DARK, STONE)
	b.box(Vector3(0.2, 0.058, 0.1), Vector3(0.07, 0.012, 0.056), STONE)
	b.box(Vector3(0.2, 0.021, 0.1355), Vector3(0.026, 0.032, 0.003), ARCH)
	b.box(Vector3(0.2, 0.034, 0.1375), Vector3(0.026, 0.002, 0.002), IRON_LIGHT)
	for k in 3:
		var bank: Vector3 = [Vector3(0.148, 0.0, 0.1), Vector3(0.252, 0.0, 0.1), Vector3(0.2, 0.0, 0.062)][k]
		var size: Vector3 = [Vector3(0.028, 0.06, 0.1), Vector3(0.028, 0.06, 0.1), Vector3(0.14, 0.06, 0.026)][k]
		b.box(bank + Vector3(0, 0.036, 0), size * Vector3(1.0, 0.8, 1.0), EARTH, GRASS)
	b.pole(Vector3(0.285, FLOOR, 0.16), Vector3(0.285, 0.115, 0.16), 0.0026, WOOD)
	b.quad(Vector3(0.285, 0.115, 0.16), Vector3(0.245, 0.108, 0.16), Vector3(0.245, 0.09, 0.16), Vector3(0.285, 0.096, 0.16), DANGER, Vector3(0.265, 0.1, 1.0))
	b.quad(Vector3(0.285, 0.115, 0.16), Vector3(0.245, 0.108, 0.16), Vector3(0.245, 0.09, 0.16), Vector3(0.285, 0.096, 0.16), DANGER.darkened(0.08), Vector3(0.265, 0.1, -1.0))
	var kegs := 6 if working else 2
	for k in kegs:
		_keg(b, Vector3(0.17 + 0.025 * (k % 3), FLOOR, 0.16 + 0.024 * (k / 3)))

## The yard: the finished cannon with its shot, the counterweight siege engine, the jib crane and, working, a casting on the hook.
static func _yard_works(b: M7MeshBuilder, working: bool) -> void:
	# the cannon: an iron barrel with a swelling muzzle on a timber carriage with two wheels, its muzzle toward the gate, and a pyramid of shot
	var cx := -0.17
	b.box(Vector3(cx, 0.022, 0.12), Vector3(0.03, 0.01, 0.09), WOOD_LIGHT)
	b.pole(Vector3(cx, 0.037, 0.082), Vector3(cx, 0.037, 0.172), 0.0085, IRON, 8)
	b.pole(Vector3(cx, 0.037, 0.172), Vector3(cx, 0.037, 0.18), 0.0105, IRON_LIGHT, 8)
	_wheel(b, Vector3(cx - 0.024, 0.026, 0.12), 0.02)
	_wheel(b, Vector3(cx + 0.024, 0.026, 0.12), 0.02)
	b.pole(Vector3(cx, 0.022, 0.07), Vector3(cx, FLOOR, 0.04), 0.004, WOOD)
	for k in 6:
		var p := Vector3(-0.255 + 0.012 * (k % 3) + 0.006 * (k / 3), 0.012 + 0.009 * (k / 3), 0.16 + 0.011 * (k / 3))
		b.blob(p, Vector3(0.0062, 0.0062, 0.0062), 5, 2, IRON, 0.0, IRON)
	# the counterweight siege engine: a timber base, two uprights with an axle, a long arm and a box of stones as the counterweight; cocked when working, at rest when idle
	var ex := 0.0
	b.box(Vector3(ex, 0.014, 0.075), Vector3(0.1, 0.012, 0.05), WOOD)
	for sx in [-0.03, 0.03]:
		b.pole(Vector3(ex + sx, 0.02, 0.075), Vector3(ex + sx, 0.085, 0.075), 0.0045, WOOD_LIGHT)
	b.pole(Vector3(ex - 0.036, 0.084, 0.075), Vector3(ex + 0.036, 0.084, 0.075), 0.0036, WOOD)
	if working:
		b.pole(Vector3(ex, 0.12, 0.034), Vector3(ex, 0.084, 0.075), 0.0042, WOOD_LIGHT)
		b.pole(Vector3(ex, 0.084, 0.075), Vector3(ex, 0.034, 0.138), 0.0042, WOOD_LIGHT)
		b.box(Vector3(ex, 0.118, 0.03), Vector3(0.034, 0.026, 0.024), IRON)
	else:
		b.pole(Vector3(ex, 0.07, 0.04), Vector3(ex, 0.084, 0.075), 0.0042, WOOD_LIGHT)
		b.pole(Vector3(ex, 0.084, 0.075), Vector3(ex, 0.03, 0.14), 0.0042, WOOD_LIGHT)
		b.box(Vector3(ex, 0.06, 0.034), Vector3(0.034, 0.026, 0.024), IRON)
	for sx in [-0.05, 0.05]:
		_wheel(b, Vector3(ex + sx, 0.018, 0.075), 0.016)
	# the jib crane at the foundry's front corner: a mast, a jib with a brace, a rope and a hook; a casting hangs on it while working
	var mast := Vector3(-0.27, 0.0, -0.045)
	b.pole(mast + Vector3(0, FLOOR, 0), mast + Vector3(0, 0.15, 0), 0.005, WOOD_LIGHT)
	b.pole(mast + Vector3(0, 0.145, 0), mast + Vector3(0.09, 0.155, 0), 0.0036, WOOD)
	b.pole(mast + Vector3(0, 0.1, 0), mast + Vector3(0.06, 0.15, 0), 0.0026, WOOD)
	var hook := mast + Vector3(0.09, 0.155, 0)
	if working:
		b.pole(hook, hook + Vector3(0, -0.085, 0), 0.0014, IRON)
		b.pole(hook + Vector3(0, -0.098, 0), hook + Vector3(0, -0.062, 0), 0.0096, IRON_LIGHT, 8)
	else:
		b.pole(hook, hook + Vector3(0, -0.04, 0), 0.0014, IRON)
		b.prism(hook + Vector3(0, -0.046, 0), 0.004, 0.0, 0.006, 4, IRON, IRON)

## The works under construction: the wall as stubs, the footing of the foundry with scaffolding round it, stacks of blocks and timber, a treadwheel crane and the magazine's
## banks already piled.
static func _site(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1752)
	_yard(b, false)
	b.tag = PLAIN
	# the foundry's footing: a low stone plinth with the first courses started at one end
	b.box(Vector3(-0.1, FLOOR + 0.008, -0.14), Vector3(0.3, 0.016, 0.14), STONE_DARK, STONE)
	b.box(Vector3(-0.2, FLOOR + 0.03, -0.14), Vector3(0.1, 0.044, 0.14), STONE, STONE_LIGHT)
	# scaffolding: uprights and two rails on the front of the footing, with a plank walkway
	for k in 6:
		b.pole(Vector3(-0.245 + 0.06 * k, FLOOR, -0.045), Vector3(-0.245 + 0.06 * k, 0.1, -0.045), 0.0028, WOOD)
	for y in [0.04, 0.08]:
		b.pole(Vector3(-0.245, y, -0.045), Vector3(0.055, y, -0.045), 0.0024, WOOD_LIGHT)
	b.box(Vector3(-0.1, 0.0425, -0.05), Vector3(0.3, 0.003, 0.02), PLANK)
	# stacks of dressed blocks and timber in the yard
	for k in 3:
		for j in 2:
			b.box(Vector3(0.17 + 0.05 * k, FLOOR + 0.01 + 0.02 * j, 0.14), Vector3(0.042, 0.018, 0.03), STONE_LIGHT, STONE_LIGHT)
	for k in 4:
		b.box(Vector3(-0.2, FLOOR + 0.006 + 0.0125 * k, 0.15), Vector3(0.12, 0.011, 0.022), PLANK, WOOD_LIGHT)
	# the treadwheel crane: a mast, a jib, and a big wheel to walk in
	var mast := Vector3(0.1, 0.0, -0.1)
	b.pole(mast + Vector3(0, FLOOR, 0), mast + Vector3(0, 0.17, 0), 0.006, WOOD_LIGHT)
	b.pole(mast + Vector3(0, 0.16, 0), mast + Vector3(-0.1, 0.175, 0), 0.004, WOOD)
	b.pole(mast + Vector3(-0.1, 0.175, 0), mast + Vector3(-0.1, 0.11, 0), 0.0014, IRON)
	b.box(mast + Vector3(-0.1, 0.103, 0), Vector3(0.026, 0.016, 0.02), STONE_LIGHT)
	for i in 10:
		var a0 := TAU * i / 10.0
		var a1 := TAU * (i + 1) / 10.0
		var c := mast + Vector3(0.04, 0.05, 0)
		b.pole(c + Vector3(0, sin(a0) * 0.045, cos(a0) * 0.045), c + Vector3(0, sin(a1) * 0.045, cos(a1) * 0.045), 0.0032, WOOD_LIGHT)
	b.pole(mast + Vector3(0.04, 0.095, 0), mast + Vector3(0.04, 0.005, 0), 0.0022, WOOD)
	# the magazine's earth banks, piled before its vault is built, and the red flag already up
	for k in 3:
		var bank: Vector3 = [Vector3(0.148, 0.0, 0.1), Vector3(0.252, 0.0, 0.1), Vector3(0.2, 0.0, 0.062)][k]
		var size: Vector3 = [Vector3(0.028, 0.06, 0.1), Vector3(0.028, 0.06, 0.1), Vector3(0.14, 0.06, 0.026)][k]
		b.box(bank + Vector3(0, 0.036, 0), size * Vector3(1.0, 0.8, 1.0), EARTH, GRASS)
	b.box(Vector3(0.2, FLOOR + 0.006, 0.1), Vector3(0.09, 0.012, 0.07), EARTH.darkened(0.12))
	b.pole(Vector3(0.285, FLOOR, 0.16), Vector3(0.285, 0.115, 0.16), 0.0026, WOOD)
	b.quad(Vector3(0.285, 0.115, 0.16), Vector3(0.245, 0.108, 0.16), Vector3(0.245, 0.09, 0.16), Vector3(0.285, 0.096, 0.16), DANGER, Vector3(0.265, 0.1, 1.0))
	b.quad(Vector3(0.285, 0.115, 0.16), Vector3(0.245, 0.108, 0.16), Vector3(0.245, 0.09, 0.16), Vector3(0.285, 0.096, 0.16), DANGER.darkened(0.08), Vector3(0.265, 0.1, -1.0))
	# the gate piers stand already; the doors are not hung
	for sx in [-1.0, 1.0]:
		b.prism(Vector3(sx * 0.06, FLOOR, 0.25), 0.02, 0.018, 0.075, 4, STONE, STONE_LIGHT, PI / 4.0)
	return b.commit()
