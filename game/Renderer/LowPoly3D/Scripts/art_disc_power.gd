## Discovery (E6) coal, steam and manufacture: the coal mine (MIN-09), the atmospheric engine (NRG-06) and the factory (MEC-10), docs/MVP/39 §4.1, §4.2, §4.4.
##
## Not wired to the simulation yet (docs/MVP/39 §9: `discWorks` and `discWorkState` carry what stands and how it is doing); photographed in the Discovery
## gallery (LowPoly3DView.show_discovery_gallery). Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.1 tall). All three are outer
## works (docs/MVP/31a). Buildings face +Z (the door, the dock, the glazed teeth of the roof toward the village). Colours are fixed (TAG_PLAIN) except the
## owner's colour on a banner; fire and window light are TAG_EMISSIVE, so a banked building is just the same mesh with the light taken out.
##
##   coal_mine_body    a tall timber A-frame headframe over a brick-collared shaft, a winding house with a drum, a rail to a black spoil heap, a coal stack
##                     at the loading bay and a lamp hut, on a stain of coal-black ground (the wheel is separate, so a rig can turn it)
##   coal_mine_wheel   the sheave wheel with its axle, in the XY plane with the hub at the origin (placed at WHEEL_HUB above the body's origin)
##   coal_mine         the body with the wheel fixed on it, working: a full tub on the rail, a big coal stack, the lamp lit
##   coal_mine_idle    no miners staffed: an empty tub, a small stack, the lamp dark
##   coal_mine_flooded the deep seam has flooded and no engine pumps it: black water in the collar, a puddle round it, the tub gone, the lamp dark
##   coal_mine_site    the headframe going up: two raised legs, a pile of timbers and sawhorses on the stain
##   atmospheric_engine_body  a brick engine house with a tall wall the beam passes through, a squat boiler with its firebox, a tall chimney, a water tank on
##                            posts and a coal heap; the pump head at the foot of the beam's outer end
##   atmospheric_engine_beam  the great beam with its arch heads and the pump rod, in the XY plane about its pivot at the origin (placed at BEAM_PIVOT);
##                            a rig rocks it about Z
##   atmospheric_engine       the body with the beam fixed level, working: the firebox glows
##   atmospheric_engine_idle  banked: the firebox dark, no coal heap (no coal or no engineers)
##   coke_oven         FIR-12: a bank of five brick beehive ovens in a row, each with a charging hole on top and a door at the front, a chimney stack behind,
##                     a heap of coal on one side and of silver-grey coke on the other, a quench trough with water; working: the doors glow
##   coke_oven_idle    cold: the doors dark, the heaps small
##   factory           a long brick shed with a sawtooth roof whose glazed teeth face the village, a tall chimney, a power shaft and belt wheel on the end
##                     wall, a loading dock with crates and bales, a water tank on stilts and a gear sign; working: every window lit
##   factory_cold      the same with the windows dark (no power or no staff)
##   factory_site      the walls half raised inside a scaffold, bricks stacked beside it
extends RefCounted

const COAL := Color("#26262b")
const COAL_LIGHT := Color("#3d3d44")
const COAL_DARK := Color("#17171a")
const STAIN := Color("#3b3835")
const BRICK := Color("#9a4a34")
const BRICK_LIGHT := Color("#b45c42")
const BRICK_DARK := Color("#74382a")
const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const TIMBER := Color("#6b5038")
const TIMBER_DARK := Color("#4a3726")
const TIMBER_LIGHT := Color("#8a6a48")
const SLATE := Color("#4f5560")
const SLATE_LIGHT := Color("#6a717d")
const CAST := Color("#4d5057")
const CAST_LIGHT := Color("#72777f")
const ROPE := Color("#b9a68a")
const BRASS := Color("#c9973c")
const GLASS_DARK := Color("#26323d")
const GLASS_LIT := Color("#e8a64a")
const WATER := Color("#243a4c")
const WATER_LIGHT := Color("#3a5a74")
const FIRE := Color("#ff7a2e")
const LAMP := Color("#ffd27a")
const SACK := Color("#d6c7a3")
const BALE := Color("#cdbf9f")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE
const CIV := M7MeshBuilder.TAG_CIV

const SHAFT := Vector3(-0.03, 0.0, -0.02)
const WHEEL_HUB := Vector3(-0.03, 0.335, 0.0)
const WHEEL_R := 0.034
const BEAM_PIVOT := Vector3(0.062, 0.215, 0.0)
# where each works' smoke leaves it, in the mesh's own frame: the single source the Discovery rig (discovery_rig.gd) reads, and the mesh code below builds its chimneys from
const MINE_SMOKE := Vector3(-0.25, 0.115, -0.04)       # the winding house's stovepipe top
const ENGINE_SMOKE := Vector3(-0.115, 0.31, 0.04)      # the engine's chimney top
const ENGINE_VALVE := Vector3(-0.01, 0.088, 0.09)      # the boiler's safety valve (steam)
const FACTORY_SMOKE := Vector3(0.15, 0.335, -0.1)      # the factory's chimney top
const COKE_SMOKE := Vector3(0.0, 0.195, -0.085)        # the coke ovens' stack top

static func build(kit) -> void:
	kit.meshes["coal_mine_body"] = _mine(kit, "work", false)
	kit.meshes["coal_mine_wheel"] = _wheel(kit)
	kit.meshes["coal_mine"] = _mine(kit, "work", true)
	kit.meshes["coal_mine_idle"] = _mine(kit, "idle", true)
	kit.meshes["coal_mine_flooded"] = _mine(kit, "flooded", true)
	kit.meshes["coal_mine_site"] = _mine_site(kit)
	kit.meshes["atmospheric_engine_body"] = _engine(kit, true, false)
	kit.meshes["atmospheric_engine_beam"] = _beam(kit)
	kit.meshes["atmospheric_engine"] = _engine(kit, true, true)
	kit.meshes["atmospheric_engine_idle"] = _engine(kit, false, true)
	kit.meshes["coke_oven"] = _coke_oven(kit, true)
	kit.meshes["coke_oven_idle"] = _coke_oven(kit, false)
	kit.meshes["factory"] = _factory(kit, true)
	kit.meshes["factory_cold"] = _factory(kit, false)
	kit.meshes["factory_site"] = _factory_site(kit)

# ------------------------------------------------------------------ shared helpers

## A hip-free gable roof: ridge along X at height h over a box w (X) by d (Z) standing on y, with a small overhang.
static func _gable(b: M7MeshBuilder, c: Vector3, w: float, d: float, h: float, col: Color, col_back: Color, col_end: Color) -> void:
	var x0 := c.x - w * 0.5 - 0.006
	var x1 := c.x + w * 0.5 + 0.006
	var zf := c.z + d * 0.5 + 0.008
	var zb := c.z - d * 0.5 - 0.008
	var below := Vector3(c.x, c.y - 0.5, c.z)
	b.quad(Vector3(x0, c.y, zf), Vector3(x1, c.y, zf), Vector3(x1, c.y + h, c.z), Vector3(x0, c.y + h, c.z), col, below)
	b.quad(Vector3(x0, c.y, zb), Vector3(x1, c.y, zb), Vector3(x1, c.y + h, c.z), Vector3(x0, c.y + h, c.z), col_back, below)
	b.tri(Vector3(x0 + 0.006, c.y, zf - 0.008), Vector3(x0 + 0.006, c.y, zb + 0.008), Vector3(x0 + 0.006, c.y + h, c.z), col_end, Vector3(c.x, c.y + h * 0.4, c.z))
	b.tri(Vector3(x1 - 0.006, c.y, zf - 0.008), Vector3(x1 - 0.006, c.y, zb + 0.008), Vector3(x1 - 0.006, c.y + h, c.z), col_end, Vector3(c.x, c.y + h * 0.4, c.z))

## A cylinder from a to e (a round boiler, a drum, a flywheel hub): radius r, `sides` faces, both ends capped.
static func _cyl(b: M7MeshBuilder, a: Vector3, e: Vector3, r: float, sides: int, col: Color, col_end: Color) -> void:
	var dir := e - a
	var len := dir.length()
	if len < 1e-6:
		return
	var up := dir / len
	var side := up.cross(Vector3.FORWARD if absf(up.y) > 0.9 else Vector3.UP).normalized()
	var fwd := up.cross(side).normalized()
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis(side, up, fwd), a)
	b.prism(Vector3.ZERO, r, r, len, sides, col, col_end, PI / sides, 0.0, true)
	b.xf = save

## A spoked wheel in the XY plane at p (axle along Z): a rim of segments, spokes and a hub.
static func _spoked(b: M7MeshBuilder, p: Vector3, r: float, rim: float, col: Color, spokes: int = 8) -> void:
	for i in 12:
		var a0 := TAU * i / 12.0
		var a1 := TAU * (i + 1) / 12.0
		b.pole(p + Vector3(cos(a0), sin(a0), 0) * r, p + Vector3(cos(a1), sin(a1), 0) * r, rim, col, 4)
	for i in spokes / 2:
		var a := PI * i / (spokes / 2.0)
		b.pole(p - Vector3(cos(a), sin(a), 0) * r, p + Vector3(cos(a), sin(a), 0) * r, rim * 0.55, col, 3)
	b.pole(p + Vector3(0, 0, -0.012), p + Vector3(0, 0, 0.012), rim * 1.6, col, 6)

static func _coal_heap(b: M7MeshBuilder, p: Vector3, r: Vector3, lumps: int) -> void:
	b.blob(p + Vector3(0, r.y * 0.35, 0), r, 7, 3, COAL, 0.18, COAL_DARK)
	for k in lumps:
		var a := b.rnd() * TAU
		var d := 0.45 + 0.4 * b.rnd()
		b.blob(p + Vector3(cos(a) * r.x * d, r.y * (0.55 + 0.35 * (1.0 - d)), sin(a) * r.z * d), Vector3(0.012, 0.01, 0.011), 4, 2, COAL_LIGHT, 0.2, COAL)

static func _sack(b: M7MeshBuilder, p: Vector3, col: Color) -> void:
	b.blob(p + Vector3(0, 0.009, 0), Vector3(0.013, 0.011, 0.01), 5, 2, col, 0.08, col.darkened(0.12))

static func _crate(b: M7MeshBuilder, p: Vector3, s: float) -> void:
	b.box(p + Vector3(0, s * 0.5, 0), Vector3(s, s, s), TIMBER_LIGHT, TIMBER_LIGHT.lightened(0.1))
	b.box(p + Vector3(0, s * 0.5, 0), Vector3(s * 1.03, s * 0.12, s * 1.03), TIMBER_DARK)

# ------------------------------------------------------------------ coal mine

static func _wheel(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1701)
	b.tag = PLAIN
	_spoked(b, Vector3.ZERO, WHEEL_R, 0.0038, TIMBER_LIGHT)
	b.pole(Vector3(0, 0, -0.056), Vector3(0, 0, 0.056), 0.0034, CAST)
	return b.commit()

static func _frame(b: M7MeshBuilder, raised: float) -> void:
	# an A-frame in two pairs of legs (front and back), each pair leaning in from x = +-0.08 to the apex, braced across and between; `raised` is how
	# much of the height is up (1.0 finished)
	var top := 0.3 * raised
	var c := SHAFT
	for sz in [-0.05, 0.05]:
		var z: float = sz
		for sx in [-1.0, 1.0]:
			var x: float = sx
			b.pole(c + Vector3(x * 0.085, 0, z), c + Vector3(x * 0.011, top, z), 0.0075, TIMBER, 4)
		for f in [0.3, 0.62]:
			if f > raised + 0.01:
				continue
			var half := lerpf(0.085, 0.011, f / raised if raised > 0.0 else 0.0)
			b.pole(c + Vector3(-half, top * f / raised if raised > 0.0 else 0.0, z), c + Vector3(half, top * f / raised if raised > 0.0 else 0.0, z), 0.0045, TIMBER_DARK, 4)
	for f in [0.3, 0.62]:
		if f > raised + 0.01:
			continue
		var y: float = top * f / raised
		var half2 := lerpf(0.085, 0.011, f / raised)
		for sx in [-1.0, 1.0]:
			var x2: float = sx
			b.pole(c + Vector3(x2 * half2, y, -0.05), c + Vector3(x2 * half2, y, 0.05), 0.004, TIMBER_DARK, 4)
	if raised > 0.95:
		# the cap beam and axle bearings the wheel hangs between
		b.box(c + Vector3(0, top + 0.004, 0), Vector3(0.04, 0.008, 0.12), TIMBER_DARK, TIMBER)

static func _mine(kit, state: String, wheel: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1700)
	b.tag = PLAIN
	var working := state == "work"
	var flooded := state == "flooded"
	# the coal-black stain of the ground, two rings of it
	b.prism(Vector3(0, 0.0, 0.01), 0.2, 0.19, 0.004, 12, STAIN, STAIN, 0.2, 0.16)
	b.prism(Vector3(0, 0.004, 0.0), 0.12, 0.11, 0.003, 10, COAL_DARK, COAL_DARK, 0.5, 0.18)
	# the shaft: a brick collar round a dark mouth (black water in it when flooded)
	var c := SHAFT
	b.prism(c, 0.058, 0.052, 0.014, 4, BRICK, BRICK_DARK, PI / 4.0)
	b.prism(c + Vector3(0, 0.0138, 0), 0.036, 0.036, 0.002, 4, COAL_DARK, COAL_DARK, PI / 4.0)
	if flooded:
		b.prism(c + Vector3(0, 0.0152, 0), 0.034, 0.034, 0.002, 4, WATER, WATER_LIGHT, PI / 4.0)
		b.prism(Vector3(c.x + 0.012, 0.0, c.z + 0.1), 0.05, 0.048, 0.003, 8, WATER, WATER_LIGHT, 0.3, 0.12)
		b.prism(Vector3(c.x - 0.1, 0.0, c.z + 0.06), 0.03, 0.028, 0.003, 7, WATER, WATER_LIGHT, 0.8, 0.12)
	_frame(b, 1.0)
	if wheel:
		_spoked(b, WHEEL_HUB, WHEEL_R, 0.0038, TIMBER_LIGHT)
		b.pole(WHEEL_HUB + Vector3(0, 0, -0.056), WHEEL_HUB + Vector3(0, 0, 0.056), 0.0034, CAST)
	# the ropes: down the shaft from the wheel, and off the far side to the winding drum
	b.pole(WHEEL_HUB + Vector3(WHEEL_R, 0, 0.0), c + Vector3(WHEEL_R, 0.05 if not flooded else 0.03, 0.0), 0.0018, ROPE, 3)
	b.pole(WHEEL_HUB + Vector3(-WHEEL_R, 0.0, 0.0), Vector3(-0.2, 0.046, 0.0), 0.0018, ROPE, 3)
	# the cage on the rope above the shaft
	if not flooded:
		b.box(c + Vector3(WHEEL_R, 0.036, 0.0), Vector3(0.024, 0.016, 0.02), TIMBER_DARK, TIMBER)
	# the winding house on the left: brick walls, a slate roof, a drum seen through the open end, a short chimney
	var wh := Vector3(-0.22, 0.0, -0.02)
	b.box(wh + Vector3(0, 0.03, 0), Vector3(0.1, 0.06, 0.08), BRICK, BRICK_LIGHT)
	_gable(b, wh + Vector3(0, 0.06, 0), 0.1, 0.08, 0.034, SLATE, SLATE_LIGHT, BRICK_DARK)
	b.box(wh + Vector3(0, 0.026, 0.0415), Vector3(0.026, 0.044, 0.004), COAL_DARK)
	_cyl(b, wh + Vector3(0.05, 0.046, -0.026), wh + Vector3(0.05, 0.046, 0.026), 0.012, 8, TIMBER_LIGHT, TIMBER_DARK)
	b.prism(Vector3(MINE_SMOKE.x, 0.06, MINE_SMOKE.z), 0.01, 0.008, MINE_SMOKE.y - 0.06, 4, BRICK_DARK, BRICK_DARK, PI / 4.0)
	# the rail from the shaft to the spoil heap, with a tub on it
	b.pole(c + Vector3(-0.01, 0.002, 0.06), c + Vector3(0.14, 0.002, 0.12), 0.0022, CAST)
	b.pole(c + Vector3(0.018, 0.002, 0.052), c + Vector3(0.17, 0.002, 0.112), 0.0022, CAST)
	for k in 5:
		var t := 0.1 + 0.2 * k
		var p := c + Vector3(0.0, 0.0, 0.06).lerp(Vector3(0.14, 0.0, 0.12), t)
		b.box(p + Vector3(0.009, 0.0015, -0.004), Vector3(0.002, 0.003, 0.03), TIMBER_DARK)
	if not flooded:
		var tub := c + Vector3(0.07, 0.006, 0.09)
		b.prism(tub, 0.014, 0.019, 0.02, 6, CAST, CAST_LIGHT, 0.0, 0.0, true)
		if working:
			b.blob(tub + Vector3(0, 0.02, 0), Vector3(0.017, 0.011, 0.017), 5, 2, COAL, 0.2, COAL_DARK)
	# the spoil heap, big and black, with a second small one
	_coal_heap(b, Vector3(0.14, 0.0, -0.07), Vector3(0.08, 0.058, 0.065), 6)
	b.blob(Vector3(0.05, 0.012, -0.12), Vector3(0.04, 0.026, 0.035), 6, 2, COAL, 0.2, COAL_DARK)
	# the loading bay: a coal stack at the front, sacks, a plank platform
	b.box(Vector3(0.0, 0.004, 0.18), Vector3(0.14, 0.006, 0.07), TIMBER_LIGHT, TIMBER_LIGHT.lightened(0.05))
	if working:
		_coal_heap(b, Vector3(-0.01, 0.006, 0.18), Vector3(0.06, 0.04, 0.035), 5)
		_sack(b, Vector3(0.07, 0.007, 0.2), SACK)
		_sack(b, Vector3(0.085, 0.007, 0.175), SACK.darkened(0.08))
	elif state == "idle":
		_coal_heap(b, Vector3(-0.01, 0.006, 0.18), Vector3(0.035, 0.018, 0.022), 2)
	# the lamp hut by the collar: a little timber box; the lamp is lit while the mine works
	b.box(Vector3(0.12, 0.016, 0.03), Vector3(0.026, 0.032, 0.022), TIMBER_DARK, TIMBER)
	if working:
		b.tag = EMISSIVE
		b.box(Vector3(0.12, 0.022, 0.042), Vector3(0.011, 0.012, 0.003), LAMP)
		b.tag = PLAIN
	return b.commit()

static func _mine_site(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1702)
	b.tag = PLAIN
	b.prism(Vector3(0, 0.0, 0.01), 0.22, 0.21, 0.004, 12, STAIN, STAIN, 0.2, 0.08)
	var c := SHAFT
	b.prism(c, 0.058, 0.052, 0.008, 4, BRICK, BRICK_DARK, PI / 4.0)
	_frame(b, 0.55)
	# timbers waiting on the ground, two sawhorses with a plank across, a few stakes round the shaft
	for k in 4:
		b.pole(Vector3(0.06, 0.006 + 0.007 * k, 0.1 + 0.0 * k), Vector3(0.2, 0.006 + 0.007 * k, 0.12), 0.0055, TIMBER_LIGHT if k % 2 == 0 else TIMBER, 4)
	for sx in [-0.1, 0.0]:
		var x: float = sx
		b.pole(Vector3(x - 0.02, 0.0, 0.16), Vector3(x, 0.034, 0.16), 0.0036, TIMBER_DARK, 4)
		b.pole(Vector3(x + 0.02, 0.0, 0.16), Vector3(x, 0.034, 0.16), 0.0036, TIMBER_DARK, 4)
	b.pole(Vector3(-0.11, 0.036, 0.16), Vector3(0.01, 0.036, 0.16), 0.005, TIMBER_LIGHT, 4)
	for k in 4:
		var a := TAU * k / 4.0 + 0.4
		b.pole(c + Vector3(cos(a) * 0.1, 0.0, sin(a) * 0.1), c + Vector3(cos(a) * 0.1, 0.03, sin(a) * 0.1), 0.0025, TIMBER_LIGHT, 3)
	_coal_heap(b, Vector3(0.14, 0.0, -0.07), Vector3(0.05, 0.03, 0.04), 2)
	return b.commit()

# ------------------------------------------------------------------ atmospheric engine

static func _beam(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1711)
	b.tag = PLAIN
	_beam_into(b, Vector3.ZERO)
	return b.commit()

static func _engine(kit, hot: bool, beam: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1710)
	b.tag = PLAIN
	# the stone footing and a skirt of coal dust
	b.box(Vector3(0, 0.005, 0), Vector3(0.36, 0.01, 0.24), STONE_DARK, STONE)
	# the engine house: a tall brick box (X by Z), its roof a low slate gable, the tall wall at +X carrying the beam through a slot
	var hc := Vector3(-0.04, 0.01, -0.02)
	b.box(hc + Vector3(0, 0.1, 0), Vector3(0.15, 0.2, 0.11), BRICK, BRICK_LIGHT)
	_gable(b, hc + Vector3(0, 0.2, 0), 0.15, 0.11, 0.04, SLATE, SLATE_LIGHT, BRICK_DARK)
	# tall arched windows on the front, a door at the foot, a stone string course and quoins
	for sx in [-0.04, 0.04]:
		var x: float = sx
		b.box(hc + Vector3(x, 0.115, 0.0555), Vector3(0.026, 0.07, 0.004), GLASS_DARK)
		b.box(hc + Vector3(x, 0.153, 0.0555), Vector3(0.032, 0.006, 0.005), STONE_LIGHT)
	b.box(hc + Vector3(0, 0.032, 0.0555), Vector3(0.032, 0.05, 0.004), COAL_DARK)
	b.box(hc + Vector3(0, 0.172, 0.0555), Vector3(0.152, 0.007, 0.006), STONE_LIGHT)
	for sx in [-1.0, 1.0]:
		var qx: float = sx
		for k in 6:
			b.box(hc + Vector3(qx * 0.073, 0.016 + 0.033 * k, 0.0535), Vector3(0.008, 0.02, 0.008), STONE_LIGHT if k % 2 == 0 else STONE)
	# the slot in the +X wall the beam swings through
	b.box(Vector3(BEAM_PIVOT.x + 0.014, BEAM_PIVOT.y, hc.z), Vector3(0.006, 0.05, 0.03), COAL_DARK)
	# the beam's pivot wall: a stone block on the wall top carrying the gudgeon bearing
	b.box(Vector3(BEAM_PIVOT.x, BEAM_PIVOT.y - 0.03, hc.z), Vector3(0.012, 0.012, 0.04), STONE_LIGHT, STONE)
	# the boiler: a squat brick-set drum on its side in front of the house, with the firebox door at its front
	var bo := Vector3(-0.04, 0.034, 0.1)
	b.box(bo + Vector3(0, -0.012, 0), Vector3(0.1, 0.02, 0.1), BRICK_DARK, BRICK)
	_cyl(b, bo + Vector3(-0.045, 0.0, 0.0), bo + Vector3(0.045, 0.0, 0.0), 0.034, 10, CAST, CAST_LIGHT)
	b.box(bo + Vector3(0.0, -0.012, 0.052), Vector3(0.04, 0.034, 0.012), BRICK_DARK, BRICK)
	if hot:
		b.tag = EMISSIVE
		b.box(bo + Vector3(0.0, -0.014, 0.0585), Vector3(0.022, 0.016, 0.002), FIRE)
		b.tag = PLAIN
	else:
		b.box(bo + Vector3(0.0, -0.014, 0.0585), Vector3(0.022, 0.016, 0.002), COAL_DARK)
	b.pole(bo + Vector3(0.03, 0.03, -0.01), bo + Vector3(0.03, 0.05, -0.01), 0.004, CAST, 4)
	b.blob(ENGINE_VALVE, Vector3(0.008, 0.006, 0.008), 4, 2, BRASS, 0.0)
	# the chimney: a tall square brick stack with a lighter band and a capped top, behind the boiler's left end
	b.prism(Vector3(ENGINE_SMOKE.x, 0.01, ENGINE_SMOKE.z), 0.03, 0.024, 0.085, 4, BRICK, BRICK_DARK, PI / 4.0)
	b.prism(Vector3(ENGINE_SMOKE.x, 0.095, ENGINE_SMOKE.z), 0.024, 0.016, 0.2, 4, BRICK, BRICK_DARK, PI / 4.0)
	b.prism(Vector3(ENGINE_SMOKE.x, 0.295, ENGINE_SMOKE.z), 0.021, 0.018, 0.008, 4, STONE_LIGHT, STONE, PI / 4.0)
	# the water tank on four posts, round the back right, and a pipe from it
	for sx in [-1.0, 1.0]:
		var tx: float = sx
		b.pole(Vector3(0.085 + tx * 0.016, 0.01, -0.1 - 0.012), Vector3(0.085 + tx * 0.016, 0.05, -0.1 - 0.012), 0.0034, TIMBER_DARK, 4)
		b.pole(Vector3(0.085 + tx * 0.016, 0.01, -0.1 + 0.012), Vector3(0.085 + tx * 0.016, 0.05, -0.1 + 0.012), 0.0034, TIMBER_DARK, 4)
	b.prism(Vector3(0.085, 0.05, -0.1), 0.026, 0.026, 0.028, 8, TIMBER, TIMBER_LIGHT, 0.0, 0.0, true)
	b.pole(Vector3(0.085, 0.05, -0.085), Vector3(0.04, 0.05, 0.02), 0.0024, CAST, 3)
	# the pump head where the rod comes down: a timber trunk with an iron spout, in the ground beyond the wall
	var ph := Vector3(BEAM_PIVOT.x + 0.16, 0.01, hc.z)
	b.prism(ph, 0.018, 0.014, 0.05, 6, TIMBER, TIMBER_LIGHT)
	b.pole(ph + Vector3(0, 0.04, 0), ph + Vector3(0.03, 0.036, 0.0), 0.004, CAST, 4)
	b.prism(ph + Vector3(0.042, 0.0, 0.0), 0.016, 0.016, 0.003, 6, WATER, WATER_LIGHT)
	# coal for the firebox: a heap to the left of the boiler while there is coal
	if hot:
		b.blob(Vector3(-0.15, 0.022, 0.13), Vector3(0.036, 0.022, 0.03), 6, 2, COAL, 0.2, COAL_DARK)
		for k in 3:
			b.blob(Vector3(-0.15 + 0.014 * (k - 1), 0.04, 0.128 + 0.006 * k), Vector3(0.009, 0.008, 0.008), 4, 2, COAL_LIGHT, 0.2, COAL)
	if beam:
		_beam_into(b, BEAM_PIVOT)
	return b.commit()

## The beam fixed level, drawn straight into the body's builder (the same parts as the separate beam mesh).
## The great beam (the one definition: the separate rocking mesh and the beam fixed level on the engine both come from here), about its pivot at `at`.
static func _beam_into(b: M7MeshBuilder, at: Vector3) -> void:
	var inner := -0.07
	var outer := 0.16
	for i in 8:
		var u0 := float(i) / 8.0
		var u1 := float(i + 1) / 8.0
		var x0 := lerpf(inner, outer, u0)
		var x1 := lerpf(inner, outer, u1)
		var h0 := 0.0065 + 0.0075 * sin(PI * u0)
		var h1 := 0.0065 + 0.0075 * sin(PI * u1)
		for sz in [-0.011, 0.011]:
			var z: float = sz
			b.quad(at + Vector3(x0, -h0, z), at + Vector3(x1, -h1, z), at + Vector3(x1, h1, z), at + Vector3(x0, h0, z), TIMBER if i % 2 == 0 else TIMBER_DARK, at + Vector3(x0, 0, z * 5.0))
		b.quad(at + Vector3(x0, h0, -0.011), at + Vector3(x1, h1, -0.011), at + Vector3(x1, h1, 0.011), at + Vector3(x0, h0, 0.011), TIMBER_LIGHT, at + Vector3(x0, 1.0, 0.0))
		b.quad(at + Vector3(x0, -h0, -0.011), at + Vector3(x1, -h1, -0.011), at + Vector3(x1, -h1, 0.011), at + Vector3(x0, -h0, 0.011), TIMBER_DARK, at + Vector3(x0, -1.0, 0.0))
	for u in [0.18, 0.5, 0.8]:
		var x: float = lerpf(inner, outer, u)
		b.box(at + Vector3(x, 0.0, 0.0), Vector3(0.004, 0.044, 0.026), CAST)
	b.pole(at + Vector3(0.0, 0.0, -0.022), at + Vector3(0.0, 0.0, 0.022), 0.0042, CAST, 6)
	b.box(at + Vector3(outer - 0.002, -0.026, 0.0), Vector3(0.028, 0.02, 0.02), CAST_LIGHT, CAST)
	b.box(at + Vector3(inner + 0.002, -0.026, 0.0), Vector3(0.028, 0.02, 0.02), CAST_LIGHT, CAST)
	# the pump rod hung from the outer end by its chain, down to the pump head
	b.pole(at + Vector3(outer, -0.03, 0.0), at + Vector3(outer, -0.13, 0.0), 0.0035, CAST_LIGHT, 4)
	b.pole(at + Vector3(outer, -0.03, 0.0), at + Vector3(outer, -0.045, 0.0), 0.0016, ROPE, 3)

# ------------------------------------------------------------------ factory

const FL := 0.4    # length along X
const FD := 0.2    # depth along Z
const FW := 0.075  # wall height
const TEETH := 3

static func _factory(kit, lit: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1720 if lit else 1721)
	b.tag = PLAIN
	var gl: Color = GLASS_LIT if lit else GLASS_DARK
	# a stone plinth and the brick body
	b.box(Vector3(0, 0.005, 0), Vector3(FL + 0.03, 0.01, FD + 0.03), STONE_DARK, STONE)
	b.box(Vector3(0, 0.01 + FW * 0.5, 0), Vector3(FL, FW, FD), BRICK, BRICK_LIGHT)
	b.box(Vector3(0, 0.0125 + FW, 0), Vector3(FL + 0.008, 0.005, FD + 0.008), STONE_LIGHT, STONE)
	var wall := 0.01 + FW + 0.005
	# the sawtooth roof: TEETH parallel teeth across the depth, each a slate slope rising toward the back and a vertical glazed face toward +Z
	var tw := FD / TEETH
	var th := 0.036
	for t in TEETH:
		var z0 := -FD * 0.5 + tw * t
		var z1 := z0 + tw
		var below := Vector3(0, wall - 0.5, (z0 + z1) * 0.5)
		b.quad(Vector3(-FL * 0.5, wall, z0), Vector3(FL * 0.5, wall, z0), Vector3(FL * 0.5, wall + th, z1), Vector3(-FL * 0.5, wall + th, z1), SLATE if t % 2 == 0 else SLATE_LIGHT, below)
		b.tri(Vector3(-FL * 0.5, wall, z0), Vector3(-FL * 0.5, wall, z1), Vector3(-FL * 0.5, wall + th, z1), BRICK_DARK, Vector3(0, wall + th * 0.3, (z0 + z1) * 0.5))
		b.tri(Vector3(FL * 0.5, wall, z0), Vector3(FL * 0.5, wall, z1), Vector3(FL * 0.5, wall + th, z1), BRICK_DARK, Vector3(0, wall + th * 0.3, (z0 + z1) * 0.5))
		# the glazed face: a row of panes in a timber frame
		var n := 7
		for k in n:
			var xa := lerpf(-FL * 0.5 + 0.008, FL * 0.5 - 0.008, float(k) / n)
			var xb := lerpf(-FL * 0.5 + 0.008, FL * 0.5 - 0.008, float(k + 1) / n) - 0.004
			if lit:
				b.tag = EMISSIVE
			b.quad(Vector3(xa, wall + 0.004, z1), Vector3(xb, wall + 0.004, z1), Vector3(xb, wall + th - 0.003, z1), Vector3(xa, wall + th - 0.003, z1), gl, Vector3(0, wall + th * 0.5, z1 - 1.0))
			b.tag = PLAIN
		b.box(Vector3(0, wall + 0.002, z1), Vector3(FL, 0.004, 0.004), TIMBER_DARK)
		b.box(Vector3(0, wall + th - 0.002, z1), Vector3(FL, 0.004, 0.005), TIMBER_DARK)
	# the front wall (+Z): a row of tall windows, the loading door, a stone course under the eaves
	var zf := FD * 0.5 + 0.002
	for k in 6:
		var wx := -0.16 + 0.065 * k
		if absf(wx - 0.1) < 0.04:
			continue
		if lit:
			b.tag = EMISSIVE
		b.box(Vector3(wx, 0.01 + FW * 0.55, zf), Vector3(0.034, 0.044, 0.003), gl)
		b.tag = PLAIN
		b.box(Vector3(wx, 0.01 + FW * 0.55 + 0.026, zf + 0.0004), Vector3(0.04, 0.006, 0.004), STONE_LIGHT)
		b.box(Vector3(wx, 0.01 + FW * 0.55 - 0.026, zf + 0.0004), Vector3(0.04, 0.005, 0.004), STONE)
	b.box(Vector3(0.1, 0.01 + 0.027, zf), Vector3(0.05, 0.054, 0.004), COAL_DARK)
	b.box(Vector3(0.1, 0.01 + 0.058, zf + 0.001), Vector3(0.058, 0.007, 0.005), STONE_LIGHT)
	# windows on the two end walls
	for sx in [-1.0, 1.0]:
		var ex: float = sx
		for sz in [-0.05, 0.05]:
			var z: float = sz
			if lit:
				b.tag = EMISSIVE
			b.box(Vector3(ex * (FL * 0.5 + 0.002), 0.01 + FW * 0.55, z), Vector3(0.003, 0.044, 0.034), gl)
			b.tag = PLAIN
	# the tall square chimney at the back right: a stone foot, then a tapering shaft in bands, a corbelled cap
	var cx := FACTORY_SMOKE.x
	var cz := FACTORY_SMOKE.z
	b.prism(Vector3(cx, 0.0125 + FW, cz), 0.04, 0.034, 0.03, 4, BRICK_DARK, BRICK_DARK, PI / 4.0)
	b.prism(Vector3(cx, 0.0425 + FW, cz), 0.034, 0.025, 0.2, 4, BRICK, BRICK_DARK, PI / 4.0)
	for k in 3:
		b.prism(Vector3(cx, 0.07 + FW + 0.065 * k, cz), 0.0305 - 0.0035 * k, 0.0305 - 0.0035 * k, 0.005, 4, BRICK_LIGHT, BRICK_LIGHT, PI / 4.0)
	b.prism(Vector3(cx, 0.2425 + FW, cz), 0.028, 0.03, 0.01, 4, STONE_LIGHT, STONE, PI / 4.0)
	# the power shaft: a long iron pole through the left end wall to a big belt wheel and a small drive wheel on a post
	b.pole(Vector3(-FL * 0.5 - 0.05, 0.05, -0.02), Vector3(-FL * 0.5 + 0.01, 0.05, -0.02), 0.0055, CAST, 6)
	b.pole(Vector3(-FL * 0.5 - 0.05, 0.0, 0.0), Vector3(-FL * 0.5 - 0.05, 0.05, -0.02), 0.0038, TIMBER_DARK, 4)
	b.pole(Vector3(-FL * 0.5 - 0.05, 0.0, -0.04), Vector3(-FL * 0.5 - 0.05, 0.05, -0.02), 0.0038, TIMBER_DARK, 4)
	_spoked_x(b, Vector3(-FL * 0.5 - 0.035, 0.05, -0.02), 0.04, 0.0036, CAST_LIGHT)
	# the loading dock on the right of the front: a plank deck with crates and bales, a hoist arm over the door
	b.box(Vector3(0.1, 0.012, FD * 0.5 + 0.05), Vector3(0.12, 0.012, 0.08), TIMBER_LIGHT, TIMBER_LIGHT.lightened(0.06))
	_crate(b, Vector3(0.07, 0.018, FD * 0.5 + 0.05), 0.026)
	_crate(b, Vector3(0.105, 0.018, FD * 0.5 + 0.06), 0.022)
	_crate(b, Vector3(0.105, 0.04, FD * 0.5 + 0.06), 0.018)
	b.blob(Vector3(0.14, 0.03, FD * 0.5 + 0.045), Vector3(0.016, 0.014, 0.014), 5, 2, BALE, 0.08, BALE.darkened(0.12))
	b.blob(Vector3(0.13, 0.03, FD * 0.5 + 0.075), Vector3(0.014, 0.013, 0.013), 5, 2, BALE.darkened(0.06), 0.08, BALE.darkened(0.16))
	b.pole(Vector3(0.1, 0.06, zf), Vector3(0.1, 0.095, zf + 0.05), 0.0035, TIMBER_DARK, 4)
	b.pole(Vector3(0.1, 0.092, zf + 0.05), Vector3(0.1, 0.06, zf + 0.05), 0.0012, ROPE, 3)
	# the water tank on stilts beside the left end, and a pipe to the wall
	for sx in [-1.0, 1.0]:
		var tx: float = sx
		for sz in [-1.0, 1.0]:
			var tz: float = sz
			b.pole(Vector3(-0.16 + tx * 0.016, 0.0, 0.14 + tz * 0.012), Vector3(-0.16 + tx * 0.016, 0.05, 0.14 + tz * 0.012), 0.0032, TIMBER_DARK, 4)
	b.prism(Vector3(-0.16, 0.05, 0.14), 0.026, 0.026, 0.028, 8, TIMBER, TIMBER_LIGHT, 0.0, 0.0, true)
	b.pole(Vector3(-0.16, 0.05, 0.13), Vector3(-0.16, 0.06, FD * 0.5), 0.0022, CAST, 3)
	# the gear sign over the roof's front edge: a brass disc with eight teeth, set above the glazing
	var gs := Vector3(-0.08, wall + th + 0.05, FD * 0.5 + 0.002)
	b.pole(Vector3(-0.08, wall + th, FD * 0.5), Vector3(-0.08, wall + th + 0.036, FD * 0.5), 0.0024, CAST, 4)
	for k in 8:
		var a := TAU * k / 8.0
		b.box(gs + Vector3(cos(a) * 0.017, sin(a) * 0.017, 0.0), Vector3(0.007, 0.007, 0.005), BRASS)
	for k in 12:
		var a0 := TAU * k / 12.0
		var a1 := TAU * (k + 1) / 12.0
		b.tri(gs, gs + Vector3(cos(a0), sin(a0), 0) * 0.014, gs + Vector3(cos(a1), sin(a1), 0) * 0.014, BRASS.lightened(0.08), gs + Vector3(0, 0, -1.0))
	return b.commit()

## A spoked wheel in the YZ plane (axle along X), for the factory's belt wheel.
static func _spoked_x(b: M7MeshBuilder, p: Vector3, r: float, rim: float, col: Color) -> void:
	for i in 12:
		var a0 := TAU * i / 12.0
		var a1 := TAU * (i + 1) / 12.0
		b.pole(p + Vector3(0, sin(a0), cos(a0)) * r, p + Vector3(0, sin(a1), cos(a1)) * r, rim, col, 4)
	for i in 4:
		var a := PI * i / 4.0
		b.pole(p - Vector3(0, sin(a), cos(a)) * r, p + Vector3(0, sin(a), cos(a)) * r, rim * 0.55, col, 3)
	b.pole(p + Vector3(-0.012, 0, 0), p + Vector3(0.012, 0, 0), rim * 1.6, col, 6)

static func _factory_site(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1722)
	b.tag = PLAIN
	b.box(Vector3(0, 0.005, 0), Vector3(FL + 0.03, 0.01, FD + 0.03), STONE_DARK, STONE)
	# the walls half raised, in brick courses
	var h := 0.034
	b.box(Vector3(0, 0.01 + h * 0.5, FD * 0.5 - 0.008), Vector3(FL, h, 0.016), BRICK, BRICK_LIGHT)
	b.box(Vector3(0, 0.01 + h * 0.5, -FD * 0.5 + 0.008), Vector3(FL, h * 1.3, 0.016), BRICK, BRICK_LIGHT)
	b.box(Vector3(-FL * 0.5 + 0.008, 0.01 + h * 0.6, 0), Vector3(0.016, h * 1.2, FD), BRICK, BRICK_LIGHT)
	b.box(Vector3(FL * 0.5 - 0.008, 0.01 + h * 0.4, 0), Vector3(0.016, h * 0.8, FD), BRICK, BRICK_LIGHT)
	# a scaffold of poles and putlogs along the front, with a ladder
	for k in 6:
		var x := -0.17 + 0.068 * k
		b.pole(Vector3(x, 0.0, FD * 0.5 + 0.03), Vector3(x, 0.12, FD * 0.5 + 0.03), 0.0032, TIMBER, 4)
	for y in [0.04, 0.08, 0.116]:
		b.pole(Vector3(-0.17, y, FD * 0.5 + 0.03), Vector3(0.17, y, FD * 0.5 + 0.03), 0.0032, TIMBER_LIGHT, 4)
	b.box(Vector3(-0.05, 0.082, FD * 0.5 + 0.03), Vector3(0.22, 0.004, 0.03), TIMBER_LIGHT, TIMBER_LIGHT.lightened(0.08))
	b.pole(Vector3(0.2, 0.0, FD * 0.5 + 0.06), Vector3(0.18, 0.116, FD * 0.5 + 0.03), 0.003, TIMBER_DARK, 4)
	# brick stacks and a mortar tub
	for k in 3:
		b.box(Vector3(0.12 + 0.002 * k, 0.012 + 0.018 * k, FD * 0.5 + 0.1), Vector3(0.05, 0.018, 0.026), BRICK_LIGHT, BRICK)
	b.prism(Vector3(0.04, 0.0, FD * 0.5 + 0.09), 0.018, 0.022, 0.02, 7, TIMBER, SACK, 0.0, 0.0, true)
	# the chimney's foot and the first courses of the stack
	b.prism(Vector3(0.15, 0.01, -0.1), 0.04, 0.034, 0.045, 4, BRICK_DARK, BRICK_DARK, PI / 4.0)
	b.prism(Vector3(0.15, 0.055, -0.1), 0.034, 0.03, 0.03, 4, BRICK, BRICK_DARK, PI / 4.0)
	return b.commit()

# ------------------------------------------------------------------ coke oven

static func _coke_oven(kit, hot: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1730 if hot else 1731)
	b.tag = PLAIN
	b.box(Vector3(0, 0.005, 0), Vector3(0.36, 0.01, 0.2), STONE_DARK, STONE)
	# the brick bank the ovens sit in, with a buttress at each end
	b.box(Vector3(0, 0.03, -0.01), Vector3(0.26, 0.04, 0.09), BRICK, BRICK_LIGHT)
	for sx in [-1.0, 1.0]:
		var x: float = sx
		b.box(Vector3(x * 0.14, 0.03, -0.01), Vector3(0.022, 0.05, 0.1), BRICK_DARK, BRICK)
	# five beehive domes in a row, a charging hole on each crown and a door at the front
	for k in 5:
		var cx := -0.1 + 0.05 * k
		b.blob(Vector3(cx, 0.044, -0.01), Vector3(0.026, 0.036, 0.04), 7, 3, BRICK_LIGHT if k % 2 == 0 else BRICK, 0.0, BRICK)
		b.prism(Vector3(cx, 0.077, -0.01), 0.007, 0.0055, 0.006, 6, COAL_DARK, CAST)
		b.box(Vector3(cx, 0.03, 0.0375), Vector3(0.022, 0.026, 0.004), BRICK_DARK)
		if hot:
			b.tag = EMISSIVE
			b.box(Vector3(cx, 0.027, 0.0402), Vector3(0.014, 0.014, 0.002), FIRE)
			b.tag = PLAIN
		else:
			b.box(Vector3(cx, 0.027, 0.0402), Vector3(0.014, 0.014, 0.002), COAL_DARK)
	# the chimney stack behind: a tall square shaft with a stone cap
	b.prism(Vector3(COKE_SMOKE.x, 0.01, COKE_SMOKE.z), 0.026, 0.02, 0.1, 4, BRICK, BRICK_DARK, PI / 4.0)
	b.prism(Vector3(COKE_SMOKE.x, 0.11, COKE_SMOKE.z), 0.02, 0.017, 0.07, 4, BRICK, BRICK_DARK, PI / 4.0)
	b.prism(Vector3(COKE_SMOKE.x, 0.18, COKE_SMOKE.z), 0.019, 0.021, 0.008, 4, STONE_LIGHT, STONE, PI / 4.0)
	# the heaps either side: raw coal left, finished coke right; and a plank apron in front
	b.box(Vector3(0, 0.012, 0.075), Vector3(0.26, 0.004, 0.05), TIMBER_LIGHT, TIMBER_LIGHT.lightened(0.05))
	_coal_heap(b, Vector3(-0.18, 0.006, 0.05), Vector3(0.045, 0.035 if hot else 0.018, 0.035), 4)
	for k in 5:
		var coke_col := Color("#8c8f96") if k % 2 == 0 else Color("#767a82")
		var sz := 0.016 if hot else 0.01
		b.blob(Vector3(0.17 + 0.012 * (k % 3) - 0.01, 0.01 + 0.011 * (k / 3), 0.05 + 0.008 * (k % 2)), Vector3(sz, sz * 0.8, sz), 4, 2, coke_col, 0.2, coke_col.darkened(0.2))
	# the quench trough: a timber box of water with a rake across it
	b.box(Vector3(0.0, 0.016, 0.1), Vector3(0.1, 0.012, 0.03), TIMBER_DARK, TIMBER)
	b.box(Vector3(0.0, 0.0225, 0.1), Vector3(0.094, 0.002, 0.024), WATER)
	b.pole(Vector3(-0.07, 0.03, 0.115), Vector3(0.08, 0.024, 0.085), 0.0022, CAST, 4)
	return b.commit()
