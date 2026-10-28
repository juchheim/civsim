## Dwellings, hearth and seasonal camp
##
## Art pass: meshes in this file REPLACE the ones mesh_kit.gd/looks_kit.gd build under the same names (`kit.meshes[name] = ...`), so
## nothing that places them changes. Keep: the mesh name, the origin (ground centre), the facing (doors/fronts toward +Z), the footprint
## (the village layout spaces dwellings by DWELL_RADIUS in world_builder.gd), and the tag meanings (TAG_CIV faces take the owner's
## colour, TAG_EMISSIVE glows). `kit` is the M7MeshKit: use kit._b(seed) for a builder and kit.WOOD, kit.THATCH ... for its palette.
##
## The dwellings climb in craft from lean-to to hall and each keeps one silhouette that reads at map distance: the lean-to a ragged
## wedge, the tent a banded cone, the pit house a turf mound, the wattle house a steep hipped cottage, the mudbrick house a stepped
## flat-roofed block, the longhouse a long hipped thatch and the hall a white block with a red pediment and portico.
extends RefCounted

const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const THATCH := Color("#c29a52")
const THATCH_DARK := Color("#a07c3e")
const HIDE := Color("#b99a72")
const HIDE_DARK := Color("#8f7354")
const DAUB := Color("#c2a57a")
const MUD := Color("#b98a5e")
const EARTH := Color("#7b6247")
const STONE := Color("#8f8a80")
const STONE_DARK := Color("#6c675f")
const BOUGH := Color("#5d6b33")
const BOUGH_DARK := Color("#46552a")
const BARK_SLAB := Color("#6b5236")
const DARK := Color("#2a2119")
const DOOR := Color("#3a2c1f")
const BRICK := Color("#c99a68")
const BRICK_LIGHT := Color("#dcb98a")
const LIME := Color("#e2cfa6")
const LIMEWASH := Color("#dbc69c")
const OCHRE := Color("#a5532f")
const TILE := Color("#a8603a")
const TURF := Color("#7f8c48")
const CLAY := Color("#a8704a")
const ASH := Color("#4a4440")
const CHAR := Color("#3b2d22")
const EMBER := Color("#ff5a1a")
const EMBER_HOT := Color("#ffa030")
const TENT_HIDE := Color("#9e7b54")    # hide panels: kept under the cloth tag's brightness clamp so the seams survive

const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const CLOTH := M7MeshBuilder.TAG_CLOTH
const GRASS := M7MeshBuilder.TAG_GRASS
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE

static func build(kit) -> void:
	kit.meshes["hearth"] = _hearth(kit)
	kit.meshes["leanto"] = _leanto(kit)
	kit.meshes["tent"] = _tent(kit)
	kit.meshes["pithouse"] = _pithouse(kit)
	kit.meshes["wattle"] = _wattle(kit)
	kit.meshes["mudbrick"] = _mudbrick(kit)
	kit.meshes["longhouse"] = _longhouse(kit)
	kit.meshes["hall"] = _hall(kit)
	kit.meshes["camp"] = _camp(kit)

# hearth: a bed of ash and glowing coals in a ring of fieldstones, a teepee of charred logs and spare wood stacked beside it
static func _hearth(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(301)
	b.prism(Vector3.ZERO, 0.036, 0.031, 0.003, 7, ASH, ASH.lightened(0.08))
	for i in 9:
		var a := TAU * (i + 0.3 * b.rnds()) / 9.0
		var s := 0.85 + 0.35 * b.rnd()
		var rr := 0.039 + 0.003 * b.rnds()
		var col := STONE.lerp(STONE_DARK, b.rnd()).lerp(STONE.lightened(0.15), 0.3 * b.rnd())
		b.blob(Vector3(cos(a) * rr, 0.005 * s, sin(a) * rr), Vector3(0.0115, 0.009, 0.0105) * s, 5, 2, col, 0.22)
	for i in 4:
		var a := TAU * i / 4.0 + 0.4
		b.pole(Vector3(cos(a) * 0.03, 0.003, sin(a) * 0.03), Vector3(cos(a) * 0.003, 0.03, sin(a) * 0.003), 0.0045,
			CHAR if i % 2 == 0 else WOOD.darkened(0.2), 3)
	b.tag = EMISSIVE
	b.prism(Vector3(0, 0.002, 0), 0.022, 0.015, 0.006, 6, EMBER, EMBER_HOT)
	for i in 3:
		var a := TAU * i / 3.0 + 1.0
		b.blob(Vector3(cos(a) * 0.019, 0.004, sin(a) * 0.019), Vector3(0.006, 0.004, 0.006), 4, 2, EMBER.darkened(0.15))
	b.tag = PLAIN
	_logs(b, Vector3(0.046, 0.0, -0.046), Vector3(0.0127, 0.0, 0.0127), Vector3(1, 0, -1), WOOD)
	return b.commit()

# lean-to: a peeled ridge pole lashed into two forked posts, a long pitch of leaned branches thatched in three shingled courses of
# bark and dry leaves from the ground behind up past the ridge, closed brush wings, and a dark hollow open to the hearth over a bed
# of dry grass and a fur; a hide drying on the roof, firewood and a basket by the door. Warm browns so it never sinks into the grass.
static func _leanto(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(305)
	var hw := 0.062                                  # half width of the pitch
	var yr := 0.08                                   # ridge height (front, open side)
	var zr := 0.04                                   # ridge line
	var zb := -0.066                                 # where the pitch meets the ground behind
	var foot := Vector3(0, -0.002, zb)
	var slope := Vector3(0, yr + 0.006, zr - 0.002) - foot
	var nrm := Vector3(0, slope.z, -slope.y).normalized()  # out of the roof: up and back
	var inside := Vector3(0, 0.02, 0.0)
	var rp := func(x: float, u: float, off: float) -> Vector3: return foot + slope * u + Vector3(x, 0, 0) + nrm * off
	# hollow: dark liners under the pitch and inside the wings
	b.quad(rp.call(-hw, 0.0, -0.004), rp.call(hw, 0.0, -0.004), rp.call(hw, 1.0, -0.004), rp.call(-hw, 1.0, -0.004),
		DARK, rp.call(0.0, 0.5, 0.05))
	# three shingled courses, oldest grey bark at the bottom to dry russet and ochre leaves at the top; ragged lower edges
	var spans := [[0.0, 0.43], [0.36, 0.75], [0.68, 1.06]]
	var offs := [0.002, 0.0055, 0.009]
	var tones := [[Color("#6f5a44"), Color("#7d6650"), Color("#655140")],
		[Color("#8e5f33"), Color("#9c6c3b"), Color("#85582f")],
		[Color("#b0844a"), Color("#a3773f"), Color("#ba8f52")]]
	var nseg := 5
	for k in 3:
		var lo: float = spans[k][0]
		var hi: float = spans[k][1]
		var off: float = offs[k]
		var low := []
		var high := []
		var ragged := 0.004 * b.rnds()
		for i in nseg + 1:
			var x := (-hw - 0.004 + ragged) + (2.0 * hw + 0.008) * i / nseg
			if i > 0 and i < nseg:
				x += 0.005 * b.rnds()
			# odd seams bulge out and hang lower, so each course reads as bundles of brush with a torn edge
			var bump := 0.0035 if i % 2 == 1 else 0.0
			var ul := lo - (0.0 if k == 0 else 0.03 * b.rnd() + (0.07 if i % 2 == 1 else 0.0))
			var uh := hi + (0.04 * b.rnd() if k == 2 else 0.0)
			low.append(rp.call(x, ul, off + 0.0015 + bump))
			high.append(rp.call(x, uh, off - 0.001 + bump * 0.5))
		for i in nseg:
			var col: Color = tones[k][(i + k) % 3].lightened(0.06 * b.rnds())
			b.quad(low[i], low[i + 1], high[i + 1], high[i], col, inside)
			if k > 0:
				# the cut edge of the course throws a dark line across the pitch
				b.quad(low[i], low[i + 1], low[i + 1] - nrm * 0.006, low[i] - nrm * 0.006, Color("#36281c"), rp.call(0.0, lo + 0.1, -0.02))
	# the pitch's thick ends
	for sx in [-1.0, 1.0]:
		var x: float = sx * (hw + 0.004)
		b.quad(rp.call(x, 0.0, 0.004), rp.call(x, 1.04, 0.011), rp.call(x, 1.0, -0.004), rp.call(x, 0.0, -0.004), Color("#4e3b29"),
			Vector3(0, 0.04, 0))
	# leaned branches lying on the thatch, their tips poking up past the ridge
	for i in 3:
		var x := -0.042 + 0.042 * i + 0.005 * b.rnds()
		var skew := 0.012 * (1.0 if i % 2 == 0 else -1.0) + 0.004 * b.rnds()
		b.pole(rp.call(x + skew, -0.015, 0.0125), rp.call(x - skew, 1.25 + 0.06 * b.rnd(), 0.0125), 0.0031,
			WOOD.darkened(0.3 + 0.1 * b.rnd()), 3)
	# peeled ridge pole in forked posts, lashed with pale cord
	b.pole(Vector3(-hw - 0.016, yr, zr), Vector3(hw + 0.016, yr + 0.002, zr), 0.0046, WOOD_LIGHT.lightened(0.06))
	for sx in [-1.0, 1.0]:
		var px: float = sx * (hw - 0.007)
		b.pole(Vector3(px, -0.001, zr + 0.005), Vector3(px - sx * 0.005, yr + 0.017, zr + 0.003), 0.0042, WOOD_LIGHT.darkened(0.05), 3)
		b.pole(Vector3(px, yr - 0.006, zr + 0.004), Vector3(px + sx * 0.012, yr + 0.019, zr + 0.003), 0.0032, WOOD_LIGHT.darkened(0.05), 3)
		b.pole(Vector3(px - 0.0055, yr, zr), Vector3(px + 0.0055, yr + 0.0005, zr), 0.0064, Color("#dcc795"), 3)
		# brush wing: ridge end down to the back foot, bulging out, its front edge on the post
		var xw: float = sx * (hw - 0.002)
		var ra := Vector3(xw, yr - 0.002, zr - 0.001)
		var fb := Vector3(xw + sx * 0.007, -0.001, zr + 0.007)
		var bk := Vector3(xw, -0.001, zb + 0.004)
		var kn := Vector3(xw + sx * 0.011, yr * 0.38, (zr + zb) * 0.5 + 0.004)
		b.tri(ra, kn, fb, BARK_SLAB, inside)
		b.tri(ra, bk, kn, BARK_SLAB.lightened(0.07), inside)
		b.tri(kn, bk, fb, BARK_SLAB.darkened(0.14), inside)
		b.tri(ra - Vector3(sx * 0.003, 0, 0), fb - Vector3(sx * 0.01, 0, 0), bk - Vector3(sx * 0.003, 0, -0.004), DARK.lightened(0.04),
			Vector3(sx, 0.03, 0))
		# brush heaped along the wing's foot
		b.blob(Vector3(sx * (hw + 0.009), 0.006, (zr + zb) * 0.5 - 0.004), Vector3(0.011, 0.013, 0.034), 5, 2, Color("#584430"), 0.25)
	# trodden earth floor in the hollow, a bed of dry grass and a fur on it, strips of meat drying under the ridge
	b.quad(Vector3(-hw + 0.002, 0.0012, zb + 0.01), Vector3(hw - 0.002, 0.0012, zb + 0.01), Vector3(hw - 0.004, 0.0012, zr + 0.01),
		Vector3(-hw + 0.004, 0.0012, zr + 0.01), Color("#3a2d21"), Vector3(0, -0.01, 0))
	for i in 2:
		var mx := -0.028 + 0.012 * i
		b.tri(Vector3(mx - 0.0026, yr - 0.004, zr + 0.001), Vector3(mx + 0.0026, yr - 0.004, zr + 0.001),
			Vector3(mx + 0.001, yr - 0.03 + 0.008 * i, zr + 0.002), Color("#8a4630").lightened(0.1 * i), Vector3(0, yr, zr - 0.02))
	b.blob(Vector3(0.002, 0.002, -0.008), Vector3(0.046, 0.0085, 0.027), 6, 2, THATCH, 0.15)
	b.quad(Vector3(-0.03, 0.011, -0.02), Vector3(0.01, 0.0115, -0.026), Vector3(0.018, 0.011, 0.01), Vector3(-0.026, 0.01, 0.014),
		Color("#6e5136"), Vector3(0, 0.0, 0))
	# a hide stretched to dry on the roof (legs and all), the brightest patch seen from behind
	var hx := 0.026
	var hu := 0.6
	var ho := 0.0125
	var hc := [rp.call(hx - 0.013, hu - 0.15, ho), rp.call(hx + 0.017, hu - 0.09, ho), rp.call(hx + 0.012, hu + 0.15, ho),
		rp.call(hx - 0.017, hu + 0.1, ho)]
	var hide := Color("#c8a77a")
	b.quad(hc[0], hc[1], hc[2], hc[3], hide, inside)
	var hcen: Vector3 = rp.call(hx, hu, ho)
	for i in 4:
		var p: Vector3 = hc[i]
		var leg: Vector3 = p + (p - hcen).normalized() * 0.011
		b.tri(p.lerp(hc[(i + 1) % 4], 0.22), p.lerp(hc[(i + 3) % 4], 0.22), leg, hide.darkened(0.08), inside)
	# by the door: firewood and a basket
	_logs(b, Vector3(0.071, 0.0, 0.03), Vector3(0.0, 0.0, 0.016), Vector3(1, 0, 0), WOOD_LIGHT)
	b.prism(Vector3(-0.042, 0.0, 0.06), 0.0085, 0.0105, 0.013, 5, THATCH_DARK, DARK, 0.3)
	return b.commit()

# hide tent (tipi): sewn hide panels on a cone of poles, the civ band, smoke flaps, a door flap pinned open, a pot and firewood
static func _tent(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(307)
	var n := 9
	var h := 0.135
	var r0 := 0.064
	var rt := 0.009
	var rings := []
	for y in [0.0, 0.05, 0.071, h]:
		rings.append([r0 + (rt - r0) * y / h, y])
	_shell(b, rings, PI * 0.5 - PI / n, TAU, n, [TENT_HIDE, Color.WHITE, TENT_HIDE.lightened(0.05)], [CLOTH, CIV, CLOTH], 0.1)
	# front panel (centred on +Z): z of its plane at a height
	var cz := cos(PI / n)
	var zf := func(y: float) -> float: return (r0 + (rt - r0) * y / h) * cz + 0.0015
	b.tri(Vector3(-0.024, 0.001, zf.call(0.0)), Vector3(0.024, 0.001, zf.call(0.0)), Vector3(0.0, 0.05, zf.call(0.05)), DOOR, Vector3(0, 0.03, 0))
	b.tag = CLOTH
	b.tri(Vector3(0.0, 0.05, zf.call(0.05) + 0.001), Vector3(0.024, 0.001, zf.call(0.0) + 0.001), Vector3(0.036, 0.016, zf.call(0.016) + 0.008),
		TENT_HIDE.lightened(0.12), Vector3(0, 0.03, 0))
	for s in [-1.0, 1.0]:
		b.tri(Vector3(s * 0.003, h - 0.004, zf.call(h - 0.004)), Vector3(s * 0.006, 0.096, zf.call(0.096)),
			Vector3(s * 0.027, 0.128, zf.call(0.128) + 0.012), TENT_HIDE.darkened(0.12), Vector3(0, 0.11, 0))
	b.tag = PLAIN
	for i in 5:
		var a := TAU * i / 5.0 + 0.3 + 0.2 * b.rnds()
		b.pole(Vector3(cos(a) * 0.006, 0.105, sin(a) * 0.006), Vector3(-cos(a) * 0.018, 0.182 + 0.01 * b.rnds(), -sin(a) * 0.018), 0.003, WOOD, 3)
	_pot(b, Vector3(0.05, 0.0, 0.046), 0.011)
	_logs(b, Vector3(-0.052, 0.0, 0.052), Vector3(0.012, 0.0, 0.012), Vector3(-0.7, 0, 0.7), WOOD)
	return b.commit()

# pit house: a sunken floor under a turf-covered cone of logs (the log ends showing at the eaves) on an earth berm, rim stones, a
# ladder out of the smoke hole, a turf-roofed entrance passage with a timber front, firewood, a pot and a hide drying on the mound
static func _pithouse(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(309)
	b.prism(Vector3.ZERO, 0.103, 0.093, 0.014, 10, EARTH.darkened(0.12), EARTH)
	b.prism(Vector3(0, 0.012, 0), 0.091, 0.087, 0.012, 10, WOOD, WOOD_LIGHT, 0.3)
	b.tag = GRASS
	b.prism(Vector3(0, 0.024, 0), 0.088, 0.058, 0.03, 10, TURF, TURF, 0.3)
	b.prism(Vector3(0, 0.054, 0), 0.058, 0.023, 0.021, 10, TURF.lightened(0.07), TURF, 0.3)
	b.tag = PLAIN
	b.prism(Vector3(0, 0.073, 0), 0.025, 0.021, 0.007, 6, WOOD, DARK)
	for i in 7:
		var a := PI * 0.5 + 0.62 + (TAU - 1.24) * i / 6.0
		var s := 0.85 + 0.3 * b.rnd()
		b.blob(Vector3(cos(a) * 0.1, 0.006, sin(a) * 0.1), Vector3(0.014, 0.011, 0.012) * s, 5, 2, STONE.lerp(STONE_DARK, b.rnd()), 0.2)
	# entrance passage
	var z0 := 0.066
	var z1 := 0.106
	var ex := 0.03
	var ey := 0.024
	var ry := 0.052
	for s in [-1.0, 1.0]:
		b.quad(Vector3(s * ex, 0, z0), Vector3(s * ex, 0, z1), Vector3(s * ex, ey, z1), Vector3(s * ex, ey, z0), EARTH.darkened(0.1), Vector3(0, ey, 0.086))
	b.tag = GRASS
	for s in [-1.0, 1.0]:
		b.quad(Vector3(s * (ex + 0.005), ey - 0.003, z0), Vector3(s * (ex + 0.005), ey - 0.003, z1 + 0.004), Vector3(0, ry, z1 + 0.004), Vector3(0, ry, z0),
			TURF if s < 0.0 else TURF.darkened(0.08), Vector3(0, 0.01, 0.086))
	b.tag = PLAIN
	b.tri(Vector3(-ex, ey, z1), Vector3(ex, ey, z1), Vector3(0, ry - 0.004, z1), WOOD_LIGHT, Vector3(0, 0.03, 0))
	b.quad(Vector3(-ex, 0, z1), Vector3(ex, 0, z1), Vector3(ex, ey, z1), Vector3(-ex, ey, z1), WOOD, Vector3(0, 0.03, 0))
	b.quad(Vector3(-0.014, 0, z1 + 0.0015), Vector3(0.014, 0, z1 + 0.0015), Vector3(0.014, 0.032, z1 + 0.0015), Vector3(-0.014, 0.032, z1 + 0.0015),
		DARK, Vector3(0, 0.03, 0))
	for s in [-1.0, 1.0]:
		b.pole(Vector3(s * 0.018, 0, z1 + 0.003), Vector3(s * 0.018, 0.036, z1 + 0.003), 0.0035, WOOD.darkened(0.1), 3)
	b.pole(Vector3(-0.025, 0.035, z1 + 0.003), Vector3(0.025, 0.035, z1 + 0.003), 0.0035, WOOD.darkened(0.1), 3)
	# ladder out of the smoke hole
	var l0 := [Vector3(-0.01, 0.045, -0.004), Vector3(0.008, 0.045, -0.004)]
	var l1 := [Vector3(-0.014, 0.135, 0.01), Vector3(0.004, 0.135, 0.01)]
	for k in 2:
		b.pole(l0[k], l1[k], 0.0033, WOOD_LIGHT, 3)
	for t in [0.5, 0.7, 0.9]:
		b.pole((l0[0] as Vector3).lerp(l1[0], t), (l0[1] as Vector3).lerp(l1[1], t), 0.0022, WOOD_LIGHT, 3)
	# a hide pegged out on the turf
	var dir := Vector3(cos(-0.7), 0, sin(-0.7))
	var across := Vector3(-dir.z, 0, dir.x)
	var hide := []
	for c in [[0.061, -0.017], [0.061, 0.017], [0.084, 0.019], [0.084, -0.016]]:
		var r: float = c[0]
		hide.append(dir * r + across * float(c[1]) + Vector3(0, 0.024 + (0.088 - r) + 0.004, 0))
	b.quad(hide[0], hide[1], hide[2], hide[3], HIDE.lightened(0.06), Vector3(0, 0, 0))
	_logs(b, Vector3(0.048, 0.0, 0.083), Vector3(0.0, 0.0, 0.016), Vector3(1, 0, 0), WOOD)
	_pot(b, Vector3(-0.045, 0.0, 0.086), 0.01)
	return b.commit()

# wattle-and-daub cottage: a stone sill, limewashed wicker walls in an oak frame, a steep hipped thatch in courses with thick eaves
# and smoke vents at the hips, a log pile under the eaves and a water jar by the door
static func _wattle(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(311)
	var wx := 0.066
	var wz := 0.05
	var wh := 0.06
	b.box(Vector3(0, 0.006, 0), Vector3(wx * 2 + 0.012, 0.012, wz * 2 + 0.012), STONE_DARK, STONE)
	b.box(Vector3(0, 0.012 + (wh - 0.012) * 0.5, 0), Vector3(wx * 2, wh - 0.012, wz * 2), LIMEWASH)
	for sx in [-wx, wx]:
		for sz in [-wz, wz]:
			b.box(Vector3(sx, 0.036, sz), Vector3(0.008, 0.052, 0.008), WOOD)
	b.box(Vector3(0, wh, wz + 0.001), Vector3(wx * 2 + 0.008, 0.006, 0.005), WOOD)
	for sx in [-0.017, 0.017]:
		b.box(Vector3(sx, 0.033, wz + 0.001), Vector3(0.005, 0.044, 0.004), WOOD)
	b.pole(Vector3(0.024, 0.014, wz + 0.002), Vector3(0.06, 0.056, wz + 0.002), 0.0025, WOOD, 3)
	b.box(Vector3(0, 0.031, wz + 0.0015), Vector3(0.026, 0.038, 0.003), DOOR)
	b.box(Vector3(-0.04, 0.04, wz + 0.0015), Vector3(0.018, 0.013, 0.003), DARK)
	b.box(Vector3(-0.055, 0.04, wz + 0.004), Vector3(0.008, 0.015, 0.002), WOOD_LIGHT)
	# hipped thatch: eave corners FL FR BR BL and the short ridge
	var ey := 0.057
	var ox := 0.085
	var oz := 0.068
	var e := [Vector3(-ox, ey, oz), Vector3(ox, ey, oz), Vector3(ox, ey, -oz), Vector3(-ox, ey, -oz)]
	var r0 := Vector3(-0.024, 0.138, 0)
	var r1 := Vector3(0.024, 0.138, 0)
	var inner := Vector3(0, 0.03, 0)
	_roof_face(b, e[0], e[1], r1, r0, THATCH, THATCH.lightened(0.09), inner)
	_roof_face(b, e[2], e[3], r0, r1, THATCH.darkened(0.1), THATCH.darkened(0.05), inner)
	_roof_face(b, e[3], e[0], r0, r0, THATCH_DARK, THATCH_DARK.lightened(0.05), inner)
	_roof_face(b, e[1], e[2], r1, r1, THATCH_DARK.darkened(0.06), THATCH_DARK, inner)
	_eaves(b, e, 0.009, THATCH_DARK.darkened(0.2))
	b.pole(r0 - Vector3(0.01, 0, 0), r1 + Vector3(0.01, 0, 0), 0.0065, THATCH_DARK.darkened(0.1))
	for k in 2:
		var r: Vector3 = r0 if k == 0 else r1
		var off := Vector3(-0.002 if k == 0 else 0.002, 0.001, 0)
		var ea: Vector3 = e[3] if k == 0 else e[1]
		var eb: Vector3 = e[0] if k == 0 else e[2]
		b.tri(r.lerp(ea, 0.2) + off, r.lerp(eb, 0.2) + off, r + off - Vector3(0, 0.003, 0), DARK, inner)
	_logs(b, Vector3(0.0765, 0.0, 0.0), Vector3(0.0, 0.0, 0.03), Vector3(1, 0, 0), WOOD)
	_pot(b, Vector3(0.03, 0.0, 0.064), 0.01)
	return b.commit()

# mudbrick house: sun-dried walls on a darker splash course, a flat earth roof behind a parapet, a stepped upper room, beam ends,
# a limewashed door surround, a ladder to the roof terrace and jars and a red drying mat up there
static func _mudbrick(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(313)
	var hx := 0.07
	var hz := 0.05
	var top := 0.08
	var roof := BRICK.darkened(0.22)
	b.box(Vector3(0, 0.007, 0), Vector3(hx * 2 + 0.006, 0.014, hz * 2 + 0.006), MUD.darkened(0.2))
	b.box(Vector3(0, top * 0.5, 0), Vector3(hx * 2, top, hz * 2), BRICK, roof)
	for sz in [-1.0, 1.0]:
		b.box(Vector3(0, top + 0.004, sz * (hz - 0.003)), Vector3(hx * 2, 0.008, 0.006), BRICK_LIGHT)
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * (hx - 0.003), top + 0.004, 0), Vector3(0.006, 0.008, hz * 2 - 0.012), BRICK_LIGHT)
	# upper room
	b.box(Vector3(-0.03, top + 0.022, -0.019), Vector3(0.07, 0.044, 0.062), BRICK_LIGHT, roof.lightened(0.08))
	b.box(Vector3(-0.03, top + 0.046, -0.019), Vector3(0.076, 0.005, 0.068), BRICK_LIGHT.lightened(0.05))
	b.box(Vector3(-0.03, top + 0.015, 0.0135), Vector3(0.016, 0.028, 0.003), DOOR)
	for i in 3:
		var x := -0.055 + 0.025 * i
		b.pole(Vector3(x, top + 0.036, 0.012), Vector3(x, top + 0.036, 0.021), 0.003, WOOD, 3)
	for i in 5:
		var x := -0.056 + 0.028 * i
		b.pole(Vector3(x, top - 0.009, hz - 0.002), Vector3(x, top - 0.009, hz + 0.011), 0.0038, WOOD, 3)
	# door, lintel and window
	b.box(Vector3(0.026, 0.03, hz + 0.001), Vector3(0.036, 0.06, 0.002), LIME)
	b.box(Vector3(0.026, 0.026, hz + 0.002), Vector3(0.024, 0.05, 0.003), DOOR)
	b.box(Vector3(0.026, 0.056, hz + 0.003), Vector3(0.04, 0.006, 0.005), WOOD)
	b.box(Vector3(-0.036, 0.048, hz + 0.0015), Vector3(0.014, 0.012, 0.003), DARK)
	# ladder to the roof
	var l0 := [Vector3(0.05, 0.0, 0.078), Vector3(0.062, 0.0, 0.078)]
	var l1 := [Vector3(0.05, 0.096, hz + 0.003), Vector3(0.062, 0.096, hz + 0.003)]
	for k in 2:
		b.pole(l0[k], l1[k], 0.0028, WOOD_LIGHT, 3)
	for t in [0.25, 0.5, 0.75]:
		b.pole((l0[0] as Vector3).lerp(l1[0], t), (l0[1] as Vector3).lerp(l1[1], t), 0.002, WOOD_LIGHT, 3)
	# terrace: jars and a drying mat
	_pot(b, Vector3(0.036, top, -0.026), 0.009)
	_pot(b, Vector3(0.052, top, -0.034), 0.008)
	b.quad(Vector3(0.012, top + 0.0012, -0.006), Vector3(0.05, top + 0.0012, -0.006), Vector3(0.05, top + 0.0012, 0.034),
		Vector3(0.012, top + 0.0012, 0.034), OCHRE, Vector3(0, 0, 0))
	return b.commit()

# longhouse: a long plank hall on a stone sill, wall posts, a hipped thatch with a sagging ridge and crossed gable horns, smoke
# vents, a porch over the door and a woodpile along the front
static func _longhouse(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(315)
	var lx := 0.148
	var lz := 0.049
	b.box(Vector3(0, 0.007, 0), Vector3(lx * 2 + 0.012, 0.014, lz * 2 + 0.01), STONE_DARK, STONE)
	b.box(Vector3(0, 0.035, 0), Vector3(lx * 2, 0.046, lz * 2), WOOD_LIGHT)
	for i in 7:
		if i == 3:
			continue
		var x := -lx + (lx * 2) * i / 6.0
		b.pole(Vector3(x, 0.006, lz + 0.002), Vector3(x, 0.062, lz + 0.002), 0.0045, WOOD)
	for sx in [-0.074, 0.074]:
		b.box(Vector3(sx, 0.041, lz + 0.0015), Vector3(0.016, 0.011, 0.003), DARK)
	b.box(Vector3(0, 0.031, lz + 0.0015), Vector3(0.03, 0.038, 0.003), DOOR)
	# roof
	var ey := 0.06
	var ez := 0.077
	var ex := 0.163
	var eave_x := [-ex, -0.055, 0.055, ex]
	var ridge := [Vector3(-0.112, 0.157, 0), Vector3(-0.04, 0.15, 0), Vector3(0.04, 0.15, 0), Vector3(0.112, 0.157, 0)]
	var inner := Vector3(0, 0.04, 0)
	for side in [1.0, -1.0]:
		for i in 3:
			var col := THATCH if side > 0.0 else THATCH.darkened(0.1)
			if i == 1:
				col = col.lightened(0.04)
			b.quad(Vector3(eave_x[i], ey, ez * side), Vector3(eave_x[i + 1], ey, ez * side), ridge[i + 1], ridge[i], col, inner)
	for sx in [-1.0, 1.0]:
		b.tri(Vector3(ex * sx, ey, ez), Vector3(ex * sx, ey, -ez), ridge[0] if sx < 0.0 else ridge[3], THATCH_DARK, inner)
	for f in [0.4, 0.72]:
		var a := Vector3(-ex, ey, ez).lerp(ridge[0], f)
		var c := Vector3(ex, ey, ez).lerp(ridge[3], f)
		b.pole(a + Vector3(0.004, 0.002, 0), c + Vector3(-0.004, 0.002, 0), 0.0028, THATCH_DARK, 3)
	_eaves(b, [Vector3(-ex, ey, ez), Vector3(ex, ey, ez), Vector3(ex, ey, -ez), Vector3(-ex, ey, -ez)], 0.009, THATCH_DARK.darkened(0.2))
	for i in 3:
		b.pole(ridge[i] - Vector3(0.006 if i == 0 else 0.0, 0, 0), ridge[i + 1] + Vector3(0.006 if i == 2 else 0.0, 0, 0), 0.0065, THATCH_DARK.darkened(0.12))
	for k in 2:
		var r: Vector3 = ridge[0] if k == 0 else ridge[3]
		var sx := -1.0 if k == 0 else 1.0
		for sz in [-1.0, 1.0]:
			var lean := 0.012 if sz > 0.0 else -0.004
			b.pole(Vector3(r.x, r.y - 0.014, 0.02 * sz), Vector3(r.x + lean * sx, r.y + 0.02, -0.008 * sz), 0.0035, WOOD, 3)
	# smoke vents: soot-dark openings under the ridge
	for side in [1.0, -1.0]:
		for vx in [-0.076, 0.076]:
			var lo := Vector3(0, ey, ez * side).lerp(Vector3(0, 0.1525, 0), 0.84) + Vector3(0, 0.003, 0)
			var hi := Vector3(0, ey, ez * side).lerp(Vector3(0, 0.1525, 0), 0.96) + Vector3(0, 0.003, 0)
			b.quad(lo + Vector3(vx - 0.009, 0, 0), lo + Vector3(vx + 0.009, 0, 0), hi + Vector3(vx + 0.008, 0, 0), hi + Vector3(vx - 0.008, 0, 0),
				THATCH_DARK.darkened(0.55), inner)
	# porch over the door
	var pz := 0.1
	var px := 0.032
	var pey := 0.048
	var pry := 0.08
	for s in [-1.0, 1.0]:
		b.quad(Vector3(s * px, pey, lz), Vector3(s * px, pey, pz), Vector3(0, pry, pz), Vector3(0, pry, lz),
			THATCH.lightened(0.05) if s < 0.0 else THATCH.darkened(0.05), Vector3(0, 0.02, 0.075))
		b.pole(Vector3(s * 0.025, 0, pz - 0.006), Vector3(s * 0.025, pey, pz - 0.006), 0.0035, WOOD)
	b.tri(Vector3(-px + 0.004, pey, pz - 0.002), Vector3(px - 0.004, pey, pz - 0.002), Vector3(0, pry - 0.004, pz - 0.002), WOOD_LIGHT, Vector3(0, 0.05, 0))
	_logs(b, Vector3(0.122, 0.0, 0.063), Vector3(0.018, 0.0, 0.0), Vector3(0, 0, 1), WOOD)
	return b.commit()

# hall: a stone podium with front steps, a limewashed body on a dark dado under a red-ochre frieze, a raised clerestory, a portico of
# six timber columns under a tiled pediment with a civ-coloured gable, civ hangings by the door, two braziers and the civ pennant
static func _hall(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(316)
	b.box(Vector3(0, 0.012, -0.005), Vector3(0.34, 0.024, 0.2), STONE, STONE.lightened(0.08))
	b.box(Vector3(0, 0.008, 0.102), Vector3(0.11, 0.016, 0.014), STONE_DARK, STONE)
	b.box(Vector3(0, 0.004, 0.115), Vector3(0.13, 0.008, 0.014), STONE_DARK, STONE)
	# body
	b.box(Vector3(0, 0.074, -0.03), Vector3(0.3, 0.1, 0.14), LIME, LIME.darkened(0.14))
	b.box(Vector3(0, 0.033, -0.03), Vector3(0.304, 0.018, 0.144), LIME.darkened(0.22))
	b.box(Vector3(0, 0.112, -0.03), Vector3(0.304, 0.012, 0.144), OCHRE)
	b.box(Vector3(0, 0.1275, -0.03), Vector3(0.31, 0.007, 0.15), LIME.lightened(0.04))
	b.box(Vector3(0, 0.152, -0.04), Vector3(0.18, 0.044, 0.09), LIME, LIME.darkened(0.14))
	b.box(Vector3(0, 0.176, -0.04), Vector3(0.19, 0.006, 0.1), LIME.lightened(0.04))
	for i in 4:
		b.box(Vector3(-0.06 + 0.04 * i, 0.153, 0.0055), Vector3(0.016, 0.02, 0.003), DARK)
	# front wall: door, windows, civ hangings
	b.box(Vector3(0, 0.062, 0.0412), Vector3(0.046, 0.072, 0.003), DOOR)
	b.box(Vector3(0, 0.1, 0.0418), Vector3(0.058, 0.007, 0.004), WOOD)
	for sx in [-0.1, 0.1]:
		b.box(Vector3(sx, 0.072, 0.0412), Vector3(0.022, 0.022, 0.003), DARK)
	b.tag = CIV
	for sx in [-0.05, 0.05]:
		b.quad(Vector3(sx - 0.01, 0.102, 0.042), Vector3(sx + 0.01, 0.102, 0.042), Vector3(sx + 0.01, 0.06, 0.042), Vector3(sx - 0.01, 0.06, 0.042),
			Color.WHITE, Vector3(0, 0.08, 0))
		b.tri(Vector3(sx - 0.01, 0.06, 0.042), Vector3(sx + 0.01, 0.06, 0.042), Vector3(sx, 0.05, 0.042), Color.WHITE, Vector3(0, 0.08, 0))
	b.tag = PLAIN
	# portico
	for i in 6:
		var x := -0.125 + 0.05 * i
		b.box(Vector3(x, 0.027, 0.072), Vector3(0.02, 0.006, 0.02), STONE_DARK)
		b.prism(Vector3(x, 0.024, 0.072), 0.0085, 0.007, 0.078, 5, WOOD_LIGHT, WOOD_LIGHT, 0.3)
		b.box(Vector3(x, 0.104, 0.072), Vector3(0.02, 0.006, 0.02), WOOD)
	b.box(Vector3(0, 0.111, 0.072), Vector3(0.28, 0.009, 0.018), WOOD)
	var pe := 0.116
	var pr := 0.158
	var pw := 0.152
	b.quad(Vector3(-pw, pe, 0.036), Vector3(-pw, pe, 0.092), Vector3(0, pr, 0.092), Vector3(0, pr, 0.036), TILE, Vector3(0, 0.08, 0.06))
	b.quad(Vector3(pw, pe, 0.036), Vector3(pw, pe, 0.092), Vector3(0, pr, 0.092), Vector3(0, pr, 0.036), TILE.darkened(0.12), Vector3(0, 0.08, 0.06))
	b.tri(Vector3(-pw + 0.012, pe, 0.089), Vector3(pw - 0.012, pe, 0.089), Vector3(0, pr - 0.004, 0.089), LIME, Vector3(0, 0.12, 0.0))
	b.tri(Vector3(-pw, pe, 0.036), Vector3(pw, pe, 0.036), Vector3(0, pr, 0.036), LIME.darkened(0.1), Vector3(0, 0.12, 0.08))
	b.tag = CIV
	b.tri(Vector3(-0.06, pe + 0.005, 0.0895), Vector3(0.06, pe + 0.005, 0.0895), Vector3(0, pe + 0.027, 0.0895), Color.WHITE, Vector3(0, 0.12, 0.0))
	b.tag = PLAIN
	# braziers flanking the steps
	for sx in [-0.085, 0.085]:
		b.prism(Vector3(sx, 0, 0.112), 0.005, 0.004, 0.02, 4, STONE_DARK, STONE_DARK)
		b.prism(Vector3(sx, 0.02, 0.112), 0.006, 0.012, 0.01, 5, STONE_DARK, DARK)
		b.tag = EMISSIVE
		b.blob(Vector3(sx, 0.03, 0.112), Vector3(0.009, 0.006, 0.009), 5, 2, EMBER)
		b.tag = PLAIN
	# pennant
	b.pole(Vector3(0.0, 0.179, -0.04), Vector3(0.0, 0.255, -0.04), 0.0035, WOOD)
	b.tag = CIV
	b.quad(Vector3(0.002, 0.252, -0.04), Vector3(0.058, 0.24, -0.04), Vector3(0.058, 0.21, -0.04), Vector3(0.002, 0.222, -0.04), Color.WHITE, Vector3(0, 0.23, 0.3))
	b.tag = PLAIN
	return b.commit()

# seasonal camp: a hide windbreak on bent poles, open to the fire, with the civ band, the pole tips crossing above and a bedroll inside
static func _camp(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(339)
	var gap := 0.8
	var rings := [[0.046, 0.0], [0.038, 0.03], [0.032, 0.042], [0.0, 0.074]]
	_shell(b, rings, PI * 0.5 + gap, TAU - gap * 2.0, 7, [HIDE_DARK, Color.WHITE, HIDE_DARK.lightened(0.06)], [CLOTH, CIV, CLOTH], 0.12,
		Vector3(0, 0, -0.008))
	for s in [-1.0, 1.0]:
		b.pole(Vector3(s * 0.006, 0.055, -0.004), Vector3(-s * 0.014, 0.1, -0.012), 0.0028, WOOD, 3)
	b.blob(Vector3(0.004, 0.005, 0.006), Vector3(0.022, 0.007, 0.012), 5, 2, HIDE.darkened(0.1))
	return b.commit()

# ------------------------------------------------------------------ helpers

## A shell of revolution: rings = [[radius, y], ...] bottom to top (radius 0 closes on `tip`), from angle a0 over `arc` in n panels.
## Tier k takes cols[k] and tags[k]; odd panels are a shade darker so the sewn seams read.
static func _shell(b: M7MeshBuilder, rings: Array, a0: float, arc: float, n: int, cols: Array, tags: Array, seam: float,
		tip := Vector3.ZERO) -> void:
	for k in rings.size() - 1:
		var ra: float = rings[k][0]
		var ya: float = rings[k][1]
		var rb: float = rings[k + 1][0]
		var yb: float = rings[k + 1][1]
		b.tag = tags[k]
		var axis := Vector3(0, (ya + yb) * 0.5, 0)
		for i in n:
			var aa := a0 + arc * i / n
			var ab := a0 + arc * (i + 1) / n
			var col: Color = cols[k]
			if i % 2 == 1:
				col = col.darkened(seam)
			var p0 := Vector3(cos(aa) * ra, ya, sin(aa) * ra)
			var p1 := Vector3(cos(ab) * ra, ya, sin(ab) * ra)
			if rb <= 0.0001:
				b.tri(p0, p1, Vector3(tip.x, yb, tip.z), col, axis)
			else:
				b.quad(p0, p1, Vector3(cos(ab) * rb, yb, sin(ab) * rb), Vector3(cos(aa) * rb, yb, sin(aa) * rb), col, axis)
	b.tag = PLAIN

## One roof pitch from the eave edge e0-e1 up to t1-t0 (t0 == t1 for a hip), laid in two thatch courses.
static func _roof_face(b: M7MeshBuilder, e0: Vector3, e1: Vector3, t1: Vector3, t0: Vector3, col_low: Color, col_high: Color,
		inner: Vector3) -> void:
	var m0 := e0.lerp(t0, 0.5)
	var m1 := e1.lerp(t1, 0.5)
	b.quad(e0, e1, m1, m0, col_low, inner)
	if t0.is_equal_approx(t1):
		b.tri(m0, m1, t0, col_high, inner)
	else:
		b.quad(m0, m1, t1, t0, col_high, inner)

## The thick cut edge of a thatch round its eave corners.
static func _eaves(b: M7MeshBuilder, corners: Array, depth: float, col: Color) -> void:
	var dy := Vector3(0, depth, 0)
	for i in corners.size():
		var p: Vector3 = corners[i]
		var q: Vector3 = corners[(i + 1) % corners.size()]
		b.quad(p, q, q - dy, p - dy, col, Vector3(0, p.y, 0))

## A small stack of firewood: two logs side by side along `half` (half length) and one on top, stepping along `across`.
static func _logs(b: M7MeshBuilder, at: Vector3, half: Vector3, across: Vector3, col: Color) -> void:
	var step := across.normalized() * 0.009
	for k in 3:
		var p := at + (step * (k - 0.5) if k < 2 else Vector3(0, 0.0075, 0)) + Vector3(0, 0.0045, 0)
		b.pole(p - half, p + half, 0.0045, col if k != 1 else col.lightened(0.1), 3)

## A squat clay pot standing on the ground at `at`.
static func _pot(b: M7MeshBuilder, at: Vector3, r: float) -> void:
	b.blob(at + Vector3(0, r * 0.85, 0), Vector3(r, r * 0.9, r), 5, 2, CLAY, 0.0, CLAY.darkened(0.2))
