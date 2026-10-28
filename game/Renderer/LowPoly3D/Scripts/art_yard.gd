## Yard structures and defenses
##
## Art pass: meshes in this file REPLACE the ones mesh_kit.gd/looks_kit.gd build under the same names (`kit.meshes[name] = ...`), so
## nothing that places them changes. Keep: the mesh name, the origin (ground centre), the facing (doors/fronts toward +Z), the footprint
## (the village layout spaces dwellings by DWELL_RADIUS in world_builder.gd), and the tag meanings (TAG_CIV faces take the owner's
## colour, TAG_EMISSIVE glows). `kit` is the M7MeshKit: use kit._b(seed) for a builder and kit.WOOD, kit.THATCH ... for its palette.
extends RefCounted

const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const BARK := Color("#4b3a2a")
const THATCH := Color("#c29a52")
const THATCH_DARK := Color("#a07c3e")
const HIDE := Color("#b99a72")
const HIDE_DARK := Color("#8f7354")
const DAUB := Color("#c2a57a")
const EARTH := Color("#7b6247")
const STONE := Color("#8f8a80")
const STONE_DARK := Color("#6c675f")
const DARK := Color("#2a2119")
const WICKER := Color("#a88758")
const ROPE := Color("#cdb57e")
const CUT := Color("#c9a879")          # freshly cut, sharpened wood
const CLAY := Color("#b5764a")
const CLAY_DARK := Color("#8c5636")
const FISH := Color("#b9c3c4")
const MEAT := Color("#8e3b2a")
const GRAIN := Color("#d9b85e")
const FLOUR := Color("#e8dfc9")
const THORNWOOD := Color("#c2b597")
const HEDGE := Color("#5a6a32")
const HEDGE_DARK := Color("#3c4622")

const PLAIN := M7MeshBuilder.TAG_PLAIN

static func build(kit) -> void:
	_storage(kit)
	_craft(kit)
	_defenses(kit)
	_water_and_fields(kit)

# ------------------------------------------------------------------ helpers

## A faceted log from p up to height h with a pale sharpened point (no hidden caps); `lean` shifts the top.
static func _stake(b: M7MeshBuilder, p: Vector3, r: float, h: float, tip: float, sides: int, col: Color, col_tip: Color,
		lean: Vector3 = Vector3.ZERO, rot: float = 0.0) -> void:
	var bot := []
	var top := []
	for i in sides:
		var a := rot + TAU * i / sides
		var d := Vector3(cos(a) * r, 0.0, sin(a) * r)
		bot.append(p + d)
		top.append(p + lean + d * 0.9 + Vector3(0, h, 0))
	var apex := p + lean * 1.2 + Vector3(0, h + tip, 0)
	var axis := p + lean * 0.5 + Vector3(0, h * 0.5, 0)
	for i in sides:
		var k := (i + 1) % sides
		b.quad(bot[i], bot[k], top[k], top[i], col if i % 2 == 0 else col.darkened(0.1), axis)
		b.tri(top[i], top[k], apex, col_tip if i % 2 == 0 else col_tip.darkened(0.12), axis)

# ------------------------------------------------------------------ storage

static func _storage(kit) -> void:
	# storage pit: a dug pit with an earth collar and rim stones, closed by a plank lid with a crossbar and a weight stone
	var b: M7MeshBuilder = kit._b(317)
	b.prism(Vector3.ZERO, 0.035, 0.031, 0.006, 7, EARTH.darkened(0.1), EARTH.darkened(0.25), 0.3, 0.08)
	for i in 5:
		var a := TAU * i / 5.0 + 0.25
		b.blob(Vector3(cos(a) * 0.029, 0.005, sin(a) * 0.029), Vector3(0.012, 0.009, 0.0105), 5, 2, STONE.lerp(STONE_DARK, b.rnd()).lightened(0.08), 0.2)
	b.prism(Vector3(0, 0.004, 0), 0.022, 0.021, 0.007, 6, WOOD, WOOD_LIGHT.lightened(0.08), 0.0)
	b.pole(Vector3(-0.022, 0.0125, 0.003), Vector3(0.022, 0.0125, -0.003), 0.0028, WOOD, 3)
	b.blob(Vector3(0.006, 0.013, 0.009), Vector3(0.007, 0.006, 0.007), 4, 2, STONE_DARK)
	kit.meshes["storagepit"] = b.commit()

	# drying rack: forked A-frames and a pole hung with split fish and meat strips, a hide thrown over one end, a fish basket below
	b = kit._b(319)
	for sx in [-0.05, 0.05]:
		b.pole(Vector3(sx, 0, -0.027), Vector3(sx, 0.086, 0.007), 0.0036, WOOD)
		b.pole(Vector3(sx, 0, 0.027), Vector3(sx, 0.086, -0.007), 0.0036, WOOD)
	b.pole(Vector3(-0.06, 0.077, 0), Vector3(0.06, 0.077, 0), 0.003, WOOD_LIGHT)
	for i in 2:
		var x0 := -0.044 + 0.021 * i
		var x1 := x0 + 0.02
		b.quad(Vector3(x0, 0.079, 0.002), Vector3(x1, 0.079, 0.002), Vector3(x1 + 0.002, 0.036, 0.021), Vector3(x0 - 0.001, 0.038, 0.021), HIDE.lightened(0.06 * i), Vector3(0, 0.0, 0))
		b.quad(Vector3(x0, 0.079, -0.002), Vector3(x1, 0.079, -0.002), Vector3(x1, 0.046, -0.019), Vector3(x0, 0.044, -0.019), HIDE_DARK, Vector3(0, 0.0, 0))
	for i in 5:
		var x := 0.003 + 0.0105 * i
		var y := 0.075
		if i % 2 == 0:
			# split fish hung by the tail
			b.tri(Vector3(x - 0.006, y, 0.003), Vector3(x + 0.006, y, 0.003), Vector3(x, y - 0.009, 0.003), FISH.darkened(0.25))
			b.quad(Vector3(x, y - 0.007, 0.003), Vector3(x + 0.0068, y - 0.022, 0.003), Vector3(x, y - 0.042, 0.003), Vector3(x - 0.0068, y - 0.022, 0.003), FISH.lightened(0.05 * i))
		else:
			b.quad(Vector3(x - 0.0034, y, 0.002), Vector3(x + 0.0034, y, 0.002), Vector3(x + 0.0024, y - 0.036, 0.003), Vector3(x - 0.0028, y - 0.032, 0.003), MEAT)
	b.prism(Vector3(0.03, 0, 0.042), 0.011, 0.013, 0.012, 6, WICKER, FISH.darkened(0.1))
	kit.meshes["dryingrack"] = b.commit()

	# granary: a round wicker-and-daub bin on stilts, a shaggy thatch hat, a door and a ladder up the front
	b = kit._b(321)
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			b.pole(Vector3(sx * 0.03, 0, sz * 0.03), Vector3(sx * 0.028, 0.036, sz * 0.028), 0.005, WOOD, 3)
	b.prism(Vector3(0, 0.034, 0), 0.047, 0.047, 0.006, 8, WOOD, WOOD_LIGHT, PI / 8.0)
	b.prism(Vector3(0, 0.04, 0), 0.038, 0.043, 0.042, 8, DAUB, DAUB, PI / 8.0)
	b.prism(Vector3(0, 0.072, 0), 0.0428, 0.0432, 0.007, 8, WICKER.darkened(0.1), WICKER, PI / 8.0)
	b.prism(Vector3(0, 0.079, 0), 0.062, 0.042, 0.022, 9, THATCH_DARK, THATCH, 0.2, 0.06)
	b.prism(Vector3(0, 0.1, 0), 0.043, 0.0, 0.05, 9, THATCH, THATCH, 0.2, 0.05)
	b.box(Vector3(0, 0.058, 0.039), Vector3(0.018, 0.024, 0.004), DARK)
	for sx in [-0.009, 0.009]:
		b.pole(Vector3(sx, 0, 0.088), Vector3(sx, 0.05, 0.04), 0.0024, WOOD_LIGHT, 3)
	for r in 3:
		var t := 0.22 + 0.27 * r
		b.pole(Vector3(-0.0095, 0.05 * t, 0.088 - 0.048 * t), Vector3(0.0095, 0.05 * t, 0.088 - 0.048 * t), 0.0017, WOOD_LIGHT, 3)
	kit.meshes["granary"] = b.commit()

# ------------------------------------------------------------------ craft

static func _craft(kit) -> void:
	# kiln: a clay dome built up in courses, a glowing fire mouth on the front, a smoking vent, firewood and fresh pots beside it
	var b: M7MeshBuilder = kit._b(323)
	var courses := [[0.0, 0.05, 0.047, 0.018], [0.018, 0.047, 0.04, 0.017], [0.035, 0.04, 0.028, 0.015], [0.05, 0.028, 0.014, 0.011]]
	for i in courses.size():
		var cr: Array = courses[i]
		var col := CLAY if i % 2 == 0 else CLAY_DARK
		b.prism(Vector3(0, cr[0], 0), cr[1], cr[2], cr[3], 8, col, col.lightened(0.08), PI / 8.0, 0.03)
	b.prism(Vector3(0, 0.058, 0), 0.012, 0.01, 0.018, 6, CLAY_DARK, DARK)
	b.box(Vector3(0, 0.013, 0.046), Vector3(0.032, 0.026, 0.016), CLAY_DARK, CLAY)
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	b.quad(Vector3(-0.009, 0.002, 0.0545), Vector3(0.009, 0.002, 0.0545), Vector3(0.007, 0.019, 0.0545), Vector3(-0.007, 0.019, 0.0545), Color("#ff6a20"), Vector3(0, 0.01, 0))
	b.prism(Vector3(0, 0.0762, 0), 0.0075, 0.0075, 0.0008, 5, Color("#ff7a20"), Color("#ffa040"))
	b.tag = PLAIN
	for i in 3:
		var y := 0.004 + 0.007 * i
		var z := -0.012 + 0.008 * i
		b.pole(Vector3(0.05, y, z - 0.024), Vector3(0.056, y, z + 0.024), 0.0042, WOOD if i % 2 == 0 else WOOD_LIGHT, 3)
	for p in [Vector3(-0.05, 0, 0.022), Vector3(-0.054, 0, 0.0)]:
		b.prism(p, 0.006, 0.009, 0.014, 5, CLAY.lightened(0.15), DARK, 0.3)
	kit.meshes["kiln"] = b.commit()

	# quern: a rotary stone with a handle peg on a flour-dusted bed stone, a pile of grain and a basket of it waiting
	b = kit._b(325)
	b.prism(Vector3.ZERO, 0.033, 0.031, 0.008, 7, STONE_DARK, FLOUR, 0.2, 0.08)
	b.prism(Vector3(0, 0.008, 0), 0.019, 0.018, 0.011, 8, STONE, STONE.lightened(0.15))
	b.prism(Vector3(0, 0.019, 0), 0.0045, 0.0045, 0.0008, 5, DARK, DARK)
	b.pole(Vector3(0.012, 0.018, 0.004), Vector3(0.014, 0.034, 0.005), 0.0022, WOOD_LIGHT, 3)
	b.blob(Vector3(0.026, 0.0, 0.02), Vector3(0.014, 0.011, 0.012), 6, 2, GRAIN, 0.1)
	b.prism(Vector3(-0.028, 0, -0.016), 0.01, 0.012, 0.012, 6, WICKER.darkened(0.1), GRAIN, 0.0)
	kit.meshes["quern"] = b.commit()

# ------------------------------------------------------------------ defenses

static func _defenses(kit) -> void:
	# thorn hedge: piled cut thorn brush over a dark living hedge, pale spines bristling out both sides (along local X, ~0.14 long)
	var b: M7MeshBuilder = kit._b(327)
	b.tag = M7MeshBuilder.TAG_FOLIAGE
	for i in 3:
		var x := -0.046 + i * 0.046 + 0.004 * b.rnds()
		b.blob(Vector3(x, 0.02, 0.003 * b.rnds()), Vector3(0.03, 0.026 + 0.004 * b.rnd(), 0.022), 5, 3, HEDGE, 0.35, HEDGE_DARK)
	for i in 3:
		var x := -0.046 + i * 0.046 + 0.023 * (i % 2)
		b.prism(Vector3(x, 0.02, 0.006 * b.rnds()), 0.014, 0.0, 0.036 + 0.01 * b.rnd(), 4, HEDGE.lightened(0.06), HEDGE, b.rnd() * TAU, 0.25)
	b.tag = PLAIN
	for i in 3:
		var x := -0.045 + i * 0.045
		var s := 1.0 if i % 2 == 0 else -1.0
		b.pole(Vector3(x - 0.03, 0.012, -0.012 * s), Vector3(x + 0.03, 0.05, 0.014 * s), 0.0024, THORNWOOD, 3)
	for i in 28:
		var x := -0.064 + i * 0.0047 + 0.002 * b.rnds()
		var sz := 1.0 if i % 2 == 0 else -1.0
		var up := i % 4 >= 2
		var y := 0.03 + 0.016 * b.rnd() if up else 0.01 + 0.022 * b.rnd()
		var base := Vector3(x, y, sz * (0.006 + 0.008 * b.rnd() if up else 0.013 + 0.005 * b.rnd()))
		var tip := base + Vector3(0.01 * b.rnds(), 0.02 + 0.01 * b.rnd() if up else 0.006 + 0.01 * b.rnd(), sz * (0.008 if up else 0.022 + 0.008 * b.rnd()))
		b.tri(base - Vector3(0.0035, 0, 0), base + Vector3(0.0035, 0, 0), tip, THORNWOOD.lightened(0.15 * b.rnd()))
	kit.meshes["thorn"] = b.commit()

	# palisade: sharpened logs of uneven height lashed with two bands of rope (along local X, ~0.125 long)
	b = kit._b(712)
	for i in 7:
		var x := -0.0535 + i * 0.01783
		var h := 0.064 + 0.03 * b.rnd()
		var lean := Vector3(0.002 * b.rnds(), 0, 0.0015 * b.rnds())
		_stake(b, Vector3(x, 0, 0), 0.0086, h, 0.02 + 0.004 * b.rnd(), 5, WOOD if i % 2 == 0 else BARK, CUT, lean, b.rnd() * TAU)
	for y in [0.022, 0.054]:
		for sz in [-0.0088, 0.0088]:
			b.pole(Vector3(-0.0625, y, sz), Vector3(0.0625, y + 0.002, sz), 0.0024, ROPE, 3)
	kit.meshes["decor_palisade"] = b.commit()

	# lookout: a splayed, X-braced timber tower on stone footings, ladder up the front, wattle parapet and a hipped thatch roof
	# (the civ banner pole set at 0.285 rises out of the roof peak)
	b = kit._b(331)
	var th := 0.232
	var rb := 0.034
	var rt := 0.022
	var corners := [Vector2(-1, -1), Vector2(1, -1), Vector2(1, 1), Vector2(-1, 1)]
	for cc in corners:
		var c: Vector2 = cc
		b.pole(Vector3(c.x * rb, 0, c.y * rb), Vector3(c.x * rt, th, c.y * rt), 0.0075, BARK.lightened(0.08), 5)
		b.box(Vector3(c.x * rb, 0.005, c.y * rb), Vector3(0.02, 0.01, 0.02), STONE, STONE.lightened(0.1))
	# X braces: both storeys on the sides, only the lower one front and back, so the frame reads as timber rather than lattice
	var levels := [0.012, 0.118, 0.226]
	for i in 4:
		var c0: Vector2 = corners[i]
		var c1: Vector2 = corners[(i + 1) % 4]
		for l in (2 if i % 2 == 1 else 1):
			var y0: float = levels[l]
			var y1: float = levels[l + 1]
			var r0 := lerpf(rb, rt, y0 / th)
			var r1 := lerpf(rb, rt, y1 / th)
			b.pole(Vector3(c0.x * r0, y0, c0.y * r0), Vector3(c1.x * r1, y1, c1.y * r1), 0.0034, WOOD, 3)
			b.pole(Vector3(c1.x * r0, y0, c1.y * r0), Vector3(c0.x * r1, y1, c0.y * r1), 0.0034, WOOD, 3)
		for y in [levels[1], 0.175]:
			var rm := lerpf(rb, rt, y / th)
			b.pole(Vector3(c0.x * rm, y, c0.y * rm), Vector3(c1.x * rm, y, c1.y * rm), 0.0036, WOOD, 3)
	var ph := 0.04
	b.box(Vector3(0, th, 0), Vector3(ph * 2.0 + 0.006, 0.009, ph * 2.0 + 0.006), WOOD, WOOD_LIGHT)
	for i in 4:
		var c0: Vector2 = corners[i] * ph
		var c1: Vector2 = corners[(i + 1) % 4] * ph
		b.quad(Vector3(c0.x, th + 0.004, c0.y), Vector3(c1.x, th + 0.004, c1.y), Vector3(c1.x, th + 0.026, c1.y), Vector3(c0.x, th + 0.026, c0.y),
			WICKER if i % 2 == 0 else WICKER.darkened(0.12))
		b.pole(Vector3(c0.x, th + 0.027, c0.y), Vector3(c1.x, th + 0.027, c1.y), 0.0026, WOOD, 3)
		b.pole(Vector3(c0.x * 0.85, th, c0.y * 0.85), Vector3(c0.x * 0.85, th + 0.05, c0.y * 0.85), 0.0034, WOOD)
	b.prism(Vector3(0, th + 0.046, 0), 0.066, 0.03, 0.024, 4, THATCH_DARK, THATCH, PI / 4.0)
	b.prism(Vector3(0, th + 0.07, 0), 0.03, 0.0, 0.03, 4, THATCH, THATCH, PI / 4.0)
	for sx in [-0.011, 0.011]:
		b.pole(Vector3(sx, 0, 0.074), Vector3(sx, th + 0.004, ph + 0.004), 0.0026, WOOD_LIGHT, 3)
	for r in 7:
		var t := (r + 0.7) / 7.6
		var z := lerpf(0.074, ph + 0.004, t)
		b.pole(Vector3(-0.011, th * t, z), Vector3(0.011, th * t, z), 0.0019, WOOD_LIGHT, 3)
	kit.meshes["lookout"] = b.commit()

	# beacon: a tall marker post in a stone cairn, crowned by a civ-coloured diamond with trailing ribbons (reads at any zoom)
	b = kit._b(332)
	b.blob(Vector3.ZERO, Vector3(0.05, 0.034, 0.046), 5, 2, STONE, 0.18)
	b.blob(Vector3(0.03, 0.0, 0.026), Vector3(0.022, 0.018, 0.02), 4, 2, STONE_DARK, 0.15)
	b.prism(Vector3(0, 0.02, 0), 0.016, 0.012, 0.3, 5, WOOD, WOOD_LIGHT)
	b.tag = M7MeshBuilder.TAG_CIV
	var mid := 0.37
	for i in 5:
		var a0 := TAU * i / 5.0
		var a1 := TAU * (i + 1) / 5.0
		var p0 := Vector3(cos(a0) * 0.05, mid, sin(a0) * 0.05)
		var p1 := Vector3(cos(a1) * 0.05, mid, sin(a1) * 0.05)
		b.tri(p0, p1, Vector3(0, 0.45, 0), Color.WHITE, Vector3(0, mid, 0))
		b.tri(p0, p1, Vector3(0, 0.31, 0), Color.WHITE, Vector3(0, mid, 0))
	b.quad(Vector3(0.012, 0.335, 0.006), Vector3(0.024, 0.335, 0.012), Vector3(0.036, 0.235, 0.03), Vector3(0.028, 0.232, 0.026), Color.WHITE)
	b.quad(Vector3(-0.01, 0.335, -0.008), Vector3(-0.02, 0.335, -0.016), Vector3(-0.03, 0.25, -0.036), Vector3(-0.024, 0.247, -0.032), Color.WHITE)
	kit.meshes["beacon"] = b.commit()

# ------------------------------------------------------------------ water and fields

static func _water_and_fields(kit) -> void:
	# weir: two arms of stakes and woven wattle funnelling into a wicker fish trap at the point (local +Z downstream)
	var b: M7MeshBuilder = kit._b(333)
	for i in 11:
		var t := float(i) / 10.0
		var x := (t - 0.5) * 0.3
		var z := absf(x) * 0.5
		b.prism(Vector3(x, -0.04, z), 0.0058, 0.0046, 0.086 + 0.016 * b.rnd() - 0.012 * absf(t - 0.5), 4, WOOD, WOOD_LIGHT, b.rnd())
	for sx in [-1.0, 1.0]:
		var e := Vector3(sx * 0.15, 0.0, 0.075)
		b.quad(Vector3(0, -0.032, 0), e + Vector3(0, -0.032, 0), e + Vector3(0, 0.002, 0), Vector3(0, 0.002, 0), WICKER.darkened(0.25))
		b.quad(Vector3(0, 0.002, 0), e + Vector3(0, 0.002, 0), e + Vector3(0, 0.03, 0), Vector3(0, 0.03, 0), WICKER)
		b.pole(Vector3(0, 0.032, 0), e + Vector3(0, 0.032, 0), 0.0028, WOOD_LIGHT, 3)
	b.blob(Vector3(0, 0.004, -0.022), Vector3(0.015, 0.015, 0.028), 6, 3, THATCH_DARK, 0.06, WICKER.darkened(0.3))
	b.prism(Vector3(0, -0.01, -0.05), 0.004, 0.003, 0.03, 4, WOOD, WOOD_LIGHT)
	kit.meshes["weir"] = b.commit()

	# fence: a woven wattle hurdle round two posts (a sheep fold); along local X, 0.13 long, rods meet the next segment end to end
	b = kit._b(335)
	for x in [-0.0325, 0.0325]:
		b.prism(Vector3(x, 0, 0), 0.0042, 0.0036, 0.046, 4, WOOD, WOOD_LIGHT, PI / 4.0)
	for band in 4:
		var y0 := 0.008 + band * 0.0076
		var y1 := y0 + 0.007
		var s := 0.0016 if band % 2 == 0 else -0.0016
		var xs := [-0.065, -0.0325, 0.0325, 0.065]
		var zs := [0.0, s, -s, 0.0]
		var col := WICKER if band % 2 == 0 else WICKER.darkened(0.16)
		for k in 3:
			b.quad(Vector3(xs[k], y0, zs[k]), Vector3(xs[k + 1], y0, zs[k + 1]), Vector3(xs[k + 1], y1, zs[k + 1]), Vector3(xs[k], y1, zs[k]), col)
	for x in [-0.049, 0.0, 0.049]:
		b.tri(Vector3(x - 0.002, 0.034, 0), Vector3(x + 0.002, 0.034, 0), Vector3(x + 0.001, 0.044, 0), WOOD_LIGHT)
	kit.meshes["fence"] = b.commit()
