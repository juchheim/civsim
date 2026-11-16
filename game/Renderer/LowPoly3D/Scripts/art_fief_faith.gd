## Faith & Fief (E5) faith: the monastery (BEL-10) and the Gothic Cathedral (BLD-12, a Wonder), docs/MVP/32 §4.5 and §4.11.
##
## Not wired to the simulation yet (docs/MVP/32 §9); photographed in the Fief gallery (LowPoly3DView.show_fief_gallery). Same units as the rest of the kit
## (1.0 = one tile column, an adult is ~0.1 tall). The monastery is an outer work, outside the wall (docs/MVP/31a, owner decision 2026-11-13); the
## Cathedral takes a Wonder hex of its own. Both face +Z (the monastery's gate, the Cathedral's west front, toward the village); TAG_CIV faces take the
## owner's colour.
##
##   monastery        staffed: a walled precinct with a church (apse, bell gable), a tall round stone tower, a cloister with a well, a dormitory range, a kitchen with a chimney, a
##                    fishpond and green garden beds, and a gate standing open
##   monastery_idle   dormant: the same precinct with the shutters closed, the gate barred, the garden brown and the kitchen cold (docs/MVP/32 §8.1: an empty
##                    monastery yields nothing and must not look alive)
##   monastery_site   queued: the foundations of the church and a range, scaffolding and stacks of stone and timber
##   gothic_cathedral_1 .. 4   the Wonder: foundations and a masons' lodge, walls rising on scaffolding, walls and twin towers up with the roof still open,
##                    finished (twin spired west towers, rose window, a crossing spire, flying buttresses, pinnacles, the owner's banners)
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const LIMESTONE := Color("#cbc3ad")        # the Cathedral's dressed stone, lighter and warmer than the village masonry
const LIMESTONE_LIGHT := Color("#ded7c2")
const LIMESTONE_DARK := Color("#a69f8a")
const SLATE := Color("#58616b")
const SLATE_DARK := Color("#444b54")
const ROOF := Color("#a8492c")
const ROOF_DARK := Color("#7f3622")
const ARCH := Color("#2a2119")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const PLASTER := Color("#e7dcc2")
const GLASS_BLUE := Color("#5b7fa6")
const GLASS_RED := Color("#a8453c")
const GOLD := Color("#c9973c")
const WATER := Color("#4a86a8")
const GARDEN := Color("#6f9a3c")
const GARDEN_DEAD := Color("#8a7a4c")
const EARTH := Color("#7b6247")
const GRASS := Color("#8ea548")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const CROP := M7MeshBuilder.TAG_CROP
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE

static func build(kit) -> void:
	kit.meshes["monastery"] = _monastery(kit, 1)
	kit.meshes["monastery_idle"] = _monastery(kit, 0)
	kit.meshes["monastery_site"] = _monastery(kit, -1)
	for stage in range(1, 5):
		kit.meshes["gothic_cathedral_%d" % stage] = _cathedral(kit, stage)

# ------------------------------------------------------------------ shared pieces

## A gable roof with its ridge along Z, centred on x = cx: eaves at `y` over half-width hx, `rise` high, from z0 to z1; the two ends are closed.
static func _roof_z(b: M7MeshBuilder, cx: float, hx: float, z0: float, z1: float, y: float, rise: float, col: Color, end_col = null) -> void:
	var inner := Vector3(cx, y - 0.01, (z0 + z1) * 0.5)
	for side in [-1.0, 1.0]:
		var c: Color = col if side > 0.0 else col.darkened(0.14)
		b.quad(Vector3(cx + side * hx, y, z0), Vector3(cx + side * hx, y, z1), Vector3(cx, y + rise, z1), Vector3(cx, y + rise, z0), c, inner)
	var ec: Color = (end_col if end_col != null else col.darkened(0.22))
	for z in [z0, z1]:
		b.tri(Vector3(cx - hx, y, z), Vector3(cx + hx, y, z), Vector3(cx, y + rise, z), ec, inner)

## The same with its ridge along X, centred on z = cz.
static func _roof_x(b: M7MeshBuilder, cz: float, hz: float, x0: float, x1: float, y: float, rise: float, col: Color, end_col = null) -> void:
	var inner := Vector3((x0 + x1) * 0.5, y - 0.01, cz)
	for side in [-1.0, 1.0]:
		var c: Color = col if side > 0.0 else col.darkened(0.14)
		b.quad(Vector3(x0, y, cz + side * hz), Vector3(x1, y, cz + side * hz), Vector3(x1, y + rise, cz), Vector3(x0, y + rise, cz), c, inner)
	var ec: Color = (end_col if end_col != null else col.darkened(0.22))
	for x in [x0, x1]:
		b.tri(Vector3(x, y, cz - hz), Vector3(x, y, cz + hz), Vector3(x, y + rise, cz), ec, inner)

## A small pinnacle: a square shaft and a pyramid.
static func _pinnacle(b: M7MeshBuilder, p: Vector3, r: float, h: float) -> void:
	b.prism(p, r, r * 0.9, h, 4, LIMESTONE, LIMESTONE_LIGHT, PI / 4.0)
	b.prism(p + Vector3(0, h, 0), r * 1.0, 0.0, r * 2.6, 4, LIMESTONE_LIGHT, LIMESTONE_LIGHT, PI / 4.0)

## A pointed (lancet) window on a face looking along `dir` (+Z, -Z, +X or -X): a dark surround and a blue glass pane under a pointed head.
static func _lancet(b: M7MeshBuilder, p: Vector3, w: float, h: float, dir: Vector3, glass: Color = GLASS_BLUE) -> void:
	var across := Vector3(-dir.z, 0.0, dir.x)
	var d := dir * 0.0026
	b.quad(p + across * (-w * 0.55) + d, p + across * (w * 0.55) + d, p + across * (w * 0.55) + Vector3(0, h, 0) + d, p + across * (-w * 0.55) + Vector3(0, h, 0) + d,
		LIMESTONE_DARK, p + dir * -1.0)
	b.tri(p + across * (-w * 0.55) + Vector3(0, h, 0) + d, p + across * (w * 0.55) + Vector3(0, h, 0) + d, p + Vector3(0, h + w * 0.8, 0) + d, LIMESTONE_DARK,
		p + dir * -1.0)
	b.quad(p + across * (-w * 0.38) + d * 1.6, p + across * (w * 0.38) + d * 1.6, p + across * (w * 0.38) + Vector3(0, h * 0.95, 0) + d * 1.6,
		p + across * (-w * 0.38) + Vector3(0, h * 0.95, 0) + d * 1.6, glass, p + dir * -1.0)
	b.tri(p + across * (-w * 0.38) + Vector3(0, h * 0.95, 0) + d * 1.6, p + across * (w * 0.38) + Vector3(0, h * 0.95, 0) + d * 1.6,
		p + Vector3(0, h * 0.95 + w * 0.62, 0) + d * 1.6, glass, p + dir * -1.0)

## A disc window (rose window) facing `dir`: a stone ring, glass and spokes.
static func _rose(b: M7MeshBuilder, p: Vector3, r: float, dir: Vector3) -> void:
	var along := Vector3(-dir.z, 0.0, dir.x)
	var up := Vector3(0, 1, 0)
	var n := 12
	for i in n:
		var a0 := TAU * i / n
		var a1 := TAU * (i + 1) / n
		var c0 := p + dir * 0.003
		var o0 := c0 + along * cos(a0) * r + up * sin(a0) * r
		var o1 := c0 + along * cos(a1) * r + up * sin(a1) * r
		b.tri(c0, o0, o1, GLASS_BLUE if i % 2 == 0 else GLASS_RED, p - dir)
		var q0 := c0 + along * cos(a0) * r * 1.22 + up * sin(a0) * r * 1.22
		var q1 := c0 + along * cos(a1) * r * 1.22 + up * sin(a1) * r * 1.22
		b.quad(o0 - dir * 0.001, o1 - dir * 0.001, q1 - dir * 0.001, q0 - dir * 0.001, LIMESTONE_DARK, p - dir)
	var tag := b.tag
	b.tag = EMISSIVE
	b.tri(p + dir * 0.0045, p + dir * 0.0045 + along * r * 0.26 + up * 0.0, p + dir * 0.0045 + up * r * 0.26, GOLD, p - dir)
	b.tag = tag

# ------------------------------------------------------------------ the monastery

## `state` 1 staffed, 0 dormant, -1 a site (queued).
static func _monastery(kit, state: int) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1150 + state + 1)
	b.tag = PLAIN
	var live := state == 1
	var site := state == -1
	# trodden ground over the precinct and a low enclosing wall with the gate in the front (+Z) side
	b.box(Vector3(0, 0.003, 0.0), Vector3(0.6, 0.008, 0.52), EARTH.lightened(0.05))
	var hw := 0.28
	var hd := 0.25
	var wall_h := 0.034
	for sz in [-1.0, 1.0]:
		var x0 := -hw
		var x1 := hw
		if sz > 0.0:
			b.box(Vector3((-hw - 0.05) * 0.5, wall_h * 0.5, hd), Vector3(hw - 0.05, wall_h, 0.016), STONE, STONE_LIGHT)
			b.box(Vector3((hw + 0.05) * 0.5, wall_h * 0.5, hd), Vector3(hw - 0.05, wall_h, 0.016), STONE, STONE_LIGHT)
		else:
			b.box(Vector3(0, wall_h * 0.5, -hd), Vector3(x1 - x0, wall_h, 0.016), STONE, STONE_LIGHT)
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * hw, wall_h * 0.5, 0), Vector3(0.016, wall_h, hd * 2.0), STONE, STONE_LIGHT)
	# the gatehouse: two piers and a lintel with a small gable over the opening; open doors when staffed, a barred gate when not
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * 0.05, 0.03, hd), Vector3(0.02, 0.06, 0.03), STONE_LIGHT, STONE)
	b.box(Vector3(0, 0.058, hd), Vector3(0.1, 0.014, 0.03), STONE_LIGHT)
	_roof_z(b, 0.0, 0.06, hd - 0.02, hd + 0.02, 0.064, 0.026, ROOF if not site else WOOD)
	if live:
		for sx in [-1.0, 1.0]:
			b.box(Vector3(sx * 0.04, 0.026, hd + 0.022), Vector3(0.004, 0.05, 0.026), WOOD_LIGHT)
	else:
		b.box(Vector3(0, 0.028, hd), Vector3(0.082, 0.052, 0.006), WOOD)
		for k in 3:
			b.box(Vector3(0, 0.014 + 0.016 * k, hd + 0.004), Vector3(0.082, 0.004, 0.004), WOOD_LIGHT)
	# the church at the back (-Z): a nave along X with an apse at the west end and a bell gable at the east end
	var cz := -0.15
	var cx0 := -0.2
	var cx1 := 0.1
	var cwh := 0.05 if not site else 0.016
	b.box(Vector3((cx0 + cx1) * 0.5, cwh * 0.5 + 0.004, cz), Vector3(cx1 - cx0, cwh, 0.1), PLASTER if not site else STONE, STONE_LIGHT)
	b.box(Vector3((cx0 + cx1) * 0.5, 0.006, cz), Vector3(cx1 - cx0 + 0.016, 0.012, 0.116), STONE_DARK, STONE)
	if not site:
		b.prism(Vector3(cx0, 0.004, cz), 0.056, 0.05, cwh, 6, PLASTER, PLASTER, PI / 6.0)
		_roof_x(b, cz, 0.062, cx0 + 0.0, cx1 + 0.006, cwh + 0.004, 0.046, ROOF, ROOF.darkened(0.2))
		b.prism(Vector3(cx0, cwh + 0.004, cz), 0.06, 0.0, 0.046, 6, ROOF, ROOF_DARK, PI / 6.0)
		# buttresses along the south wall and lancets between them
		for k in 5:
			var bx := cx0 + 0.04 + 0.052 * k
			b.box(Vector3(bx, 0.03, cz + 0.054), Vector3(0.01, 0.052, 0.012), STONE_LIGHT, STONE)
			if k < 4:
				b.box(Vector3(bx + 0.026, 0.036, cz + 0.0526), Vector3(0.012, 0.026, 0.003), ARCH if live else WOOD)
		# the bell gable at the east end: a stepped wall with an arch, a bell in it and a cross on top
		b.box(Vector3(cx1 + 0.006, 0.062, cz), Vector3(0.012, 0.1, 0.062), PLASTER, STONE_LIGHT)
		b.box(Vector3(cx1 + 0.006, 0.12, cz), Vector3(0.012, 0.02, 0.034), PLASTER)
		b.box(Vector3(cx1 + 0.0125, 0.085, cz), Vector3(0.003, 0.03, 0.02), ARCH)
		b.prism(Vector3(cx1 + 0.002, 0.07, cz), 0.009, 0.005, 0.018, 6, GOLD.darkened(0.2), GOLD)
		b.pole(Vector3(cx1 + 0.006, 0.13, cz), Vector3(cx1 + 0.006, 0.16, cz), 0.0022, WOOD)
		b.pole(Vector3(cx1 + 0.006, 0.15, cz - 0.012), Vector3(cx1 + 0.006, 0.15, cz + 0.012), 0.002, WOOD)
		# a door in the front of the nave at the cloister end
		b.box(Vector3(cx1 - 0.04, 0.026, cz + 0.0506), Vector3(0.026, 0.044, 0.004), WOOD if live else WOOD.darkened(0.15))
	# the cloister: an open garth ringed by a covered walk, a well in the middle (south of the church)
	var gx0 := -0.13
	var gx1 := 0.07
	var gz0 := -0.07
	var gz1 := 0.11
	b.box(Vector3((gx0 + gx1) * 0.5, 0.008, (gz0 + gz1) * 0.5), Vector3(gx1 - gx0, 0.004, gz1 - gz0), GRASS if live else GARDEN_DEAD.lightened(0.05))
	if not site:
		for side in range(4):
			var p0: Vector3
			var p1: Vector3
			match side:
				0:
					p0 = Vector3(gx0, 0, gz0)
					p1 = Vector3(gx1, 0, gz0)
				1:
					p0 = Vector3(gx0, 0, gz1)
					p1 = Vector3(gx1, 0, gz1)
				2:
					p0 = Vector3(gx0, 0, gz0)
					p1 = Vector3(gx0, 0, gz1)
				_:
					p0 = Vector3(gx1, 0, gz0)
					p1 = Vector3(gx1, 0, gz1)
			var along := (p1 - p0)
			var steps := int(round(along.length() / 0.03))
			for k in range(steps + 1):
				var p := p0 + along * (float(k) / steps)
				b.pole(p + Vector3(0, 0.008, 0), p + Vector3(0, 0.034, 0), 0.0032, STONE_LIGHT)
			var q0 := p0 + Vector3(0, 0.036, 0)
			var q1 := p1 + Vector3(0, 0.036, 0)
			var o := Vector3(0, 0, 0)
			if side == 0:
				o = Vector3(0, 0, -0.024)
			elif side == 1:
				o = Vector3(0, 0, 0.024)
			elif side == 2:
				o = Vector3(-0.024, 0, 0)
			else:
				o = Vector3(0.024, 0, 0)
			b.quad(q0, q1, q1 + o + Vector3(0, -0.012, 0), q0 + o + Vector3(0, -0.012, 0), ROOF.darkened(0.08), (q0 + q1) * 0.5 + Vector3(0, -0.1, 0))
	# the well: a stone ring with a little roof on two posts
	var wc := Vector3((gx0 + gx1) * 0.5, 0.0, (gz0 + gz1) * 0.5)
	b.prism(wc + Vector3(0, 0.006, 0), 0.016, 0.016, 0.016, 8, STONE, STONE_LIGHT, PI / 8.0)
	b.prism(wc + Vector3(0, 0.02, 0), 0.011, 0.011, 0.002, 8, WATER, WATER, PI / 8.0)
	if not site:
		for sx in [-0.013, 0.013]:
			b.pole(wc + Vector3(sx, 0.006, 0), wc + Vector3(sx, 0.05, 0), 0.0022, WOOD)
		_roof_x(b, wc.z, 0.02, wc.x - 0.022, wc.x + 0.022, 0.05, 0.016, ROOF)
	# the dormitory range on the east side of the cloister: two storeys of plaster under a gable roof, a stair door and rows of windows
	var dx := 0.145
	var dz0 := -0.075
	var dz1 := 0.11
	var dh := 0.06 if not site else 0.016
	b.box(Vector3(dx, dh * 0.5 + 0.004, (dz0 + dz1) * 0.5), Vector3(0.1, dh, dz1 - dz0), PLASTER if not site else STONE, STONE_LIGHT)
	if not site:
		_roof_z(b, dx, 0.062, dz0 - 0.008, dz1 + 0.008, dh + 0.004, 0.044, ROOF)
		for k in 4:
			var z := dz0 + 0.03 + 0.04 * k
			b.box(Vector3(dx - 0.0505, 0.04, z), Vector3(0.003, 0.02, 0.012), ARCH if live else WOOD)
			b.box(Vector3(dx + 0.0505, 0.04, z), Vector3(0.003, 0.02, 0.012), ARCH if live else WOOD)
			b.box(Vector3(dx - 0.0505, 0.016, z), Vector3(0.003, 0.016, 0.01), ARCH if live else WOOD)
	# the kitchen block with its chimney on the west side: a lower gable with a stack (the smoke is the war rig's, as for the forge)
	var kx := -0.215
	if not site:
		b.box(Vector3(kx, 0.025, 0.02), Vector3(0.07, 0.05, 0.09), PLASTER, STONE_LIGHT)
		_roof_x(b, 0.02, 0.054, kx - 0.04, kx + 0.04, 0.05, 0.034, ROOF)
		b.prism(Vector3(kx - 0.02, 0.066, 0.01), 0.012, 0.009, 0.05, 4, STONE, STONE_LIGHT, PI / 4.0)
		b.box(Vector3(kx + 0.0355, 0.022, 0.02), Vector3(0.003, 0.03, 0.02), ARCH if live else WOOD)
	# the fishpond and the garden beds outside the cloister, in the south-west and south-east of the precinct
	b.prism(Vector3(-0.18, 0.006, 0.165), 0.052, 0.05, 0.004, 10, WATER if live else WATER.darkened(0.25), WATER if live else WATER.darkened(0.25), 0.2)
	for k in 6:
		var a := TAU * k / 6.0
		b.blob(Vector3(-0.18 + cos(a) * 0.056, 0.008, 0.165 + sin(a) * 0.05), Vector3(0.012, 0.008, 0.01), 5, 2, STONE_LIGHT, 0.2, STONE)
	b.tag = CROP if live else PLAIN   # the crop tag takes the season's green whatever colour is given, so dead beds are plain
	for row in 3:
		for k in 2:
			var bx2 := 0.1 + 0.07 * k
			var bz := 0.12 + 0.045 * row
			b.box(Vector3(bx2, 0.012, bz), Vector3(0.056, 0.012, 0.03), GARDEN if live else GARDEN_DEAD)
	b.tag = PLAIN
	for row in 3:
		for k in 2:
			b.box(Vector3(0.1 + 0.07 * k, 0.005, 0.12 + 0.045 * row), Vector3(0.064, 0.006, 0.036), EARTH.darkened(0.2))
	# the round tower: a tall cylindrical stone tower standing apart at the east end of the church, the monastery's landmark and the one shape that is
	# visible from far off. A raised door, slit windows, four belfry openings under a conical cap with a cross. Only a stump while it is a site.
	var tx := 0.19
	var tz := -0.17
	var th := 0.27 if not site else 0.05
	b.prism(Vector3(tx, -0.004, tz), 0.044, 0.037, 0.032, 10, STONE_DARK, STONE_DARK)
	b.prism(Vector3(tx, 0.028, tz), 0.037, 0.027, th - 0.028, 10, STONE, STONE_LIGHT)
	if not site:
		for y in [0.1, 0.18]:
			var rr: float = lerpf(0.037, 0.027, (y - 0.028) / (th - 0.028)) * 1.07
			b.prism(Vector3(tx, y, tz), rr, rr, 0.006, 10, STONE_LIGHT, STONE_LIGHT)
		# a doorway well above the ground (the tower was a refuge): a dark opening with a lintel, and a short ladder against the wall
		b.box(Vector3(tx, 0.058, tz + 0.0345), Vector3(0.013, 0.024, 0.006), ARCH)
		b.box(Vector3(tx, 0.073, tz + 0.0345), Vector3(0.019, 0.004, 0.007), STONE_LIGHT)
		b.pole(Vector3(tx + 0.012, 0.0, tz + 0.05), Vector3(tx + 0.012, 0.056, tz + 0.038), 0.0022, WOOD)
		b.pole(Vector3(tx + 0.02, 0.0, tz + 0.05), Vector3(tx + 0.02, 0.056, tz + 0.038), 0.0022, WOOD)
		for k in 5:
			b.pole(Vector3(tx + 0.012, 0.01 + 0.011 * k, tz + 0.049 - 0.0026 * k), Vector3(tx + 0.02, 0.01 + 0.011 * k, tz + 0.049 - 0.0026 * k), 0.0013, WOOD_LIGHT)
		# slit windows up the shaft and four belfry openings under the cap
		for a in [0.6, 2.4, 4.2]:
			b.box(Vector3(tx + cos(a) * 0.032, 0.13, tz + sin(a) * 0.032), Vector3(0.005, 0.016, 0.005), ARCH)
			b.box(Vector3(tx + cos(a + 1.0) * 0.03, 0.16, tz + sin(a + 1.0) * 0.03), Vector3(0.005, 0.016, 0.005), ARCH)
		for dir in [Vector3(0, 0, 1), Vector3(0, 0, -1), Vector3(1, 0, 0), Vector3(-1, 0, 0)]:
			var sz2 := Vector3(0.011, 0.028, 0.005) if absf(dir.z) > 0.5 else Vector3(0.005, 0.028, 0.011)
			b.box(Vector3(tx, 0.226, tz) + dir * 0.0275, sz2, ARCH)
		b.prism(Vector3(tx, th, tz), 0.031, 0.031, 0.006, 10, STONE_LIGHT, STONE_LIGHT)
		b.prism(Vector3(tx, th + 0.006, tz), 0.03, 0.0, 0.078, 10, ROOF, ROOF_DARK)
		b.pole(Vector3(tx, th + 0.08, tz), Vector3(tx, th + 0.106, tz), 0.0022, GOLD)
		b.pole(Vector3(tx - 0.009, th + 0.097, tz), Vector3(tx + 0.009, th + 0.097, tz), 0.002, GOLD)
		# a bell glimpsed in the openings when staffed
		if live:
			b.prism(Vector3(tx, 0.222, tz), 0.008, 0.0045, 0.014, 6, GOLD.darkened(0.15), GOLD)
		else:
			b.box(Vector3(tx, 0.226, tz) + Vector3(0, 0, 0.0285), Vector3(0.012, 0.03, 0.002), WOOD)
	else:
		for sx in [-0.045, 0.0, 0.045]:
			b.pole(Vector3(tx + sx, -0.01, tz + 0.055), Vector3(tx + sx, 0.075, tz + 0.055), 0.0028, WOOD_LIGHT)
	# a dormant monastery is overgrown: weed clumps in the garth, along the paths and in the beds
	if state == 0:
		for k in 9:
			var wx := lerpf(gx0 + 0.02, gx1 - 0.02, fmod(k * 0.37, 1.0))
			var wz := lerpf(gz0 + 0.02, gz1 - 0.02, fmod(k * 0.61 + 0.2, 1.0))
			b.blob(Vector3(wx, 0.012, wz), Vector3(0.014, 0.012, 0.012), 5, 2, Color("#5c6a2c"), 0.25, Color("#7a7a3c"))
		for k in 6:
			b.blob(Vector3(0.095 + 0.04 * (k % 3), 0.022, 0.125 + 0.05 * (k / 3)), Vector3(0.012, 0.01, 0.01), 5, 2, Color("#5c6a2c"), 0.25, Color("#7a7a3c"))
	# a site: stone stacks, a timber stack and scaffolding on the church wall
	if site:
		for k in 5:
			b.blob(Vector3(-0.1 + 0.034 * k, 0.012, 0.0), Vector3(0.022, 0.014, 0.018), 5, 3, STONE_LIGHT, 0.12, STONE)
		b.box(Vector3(0.0, 0.016, 0.17), Vector3(0.12, 0.02, 0.04), WOOD_LIGHT)
		for sx in [-0.18, -0.09, 0.0, 0.09]:
			b.pole(Vector3(sx, -0.01, cz + 0.07), Vector3(sx, 0.07, cz + 0.07), 0.0032, WOOD_LIGHT)
		b.box(Vector3(-0.045, 0.04, cz + 0.07), Vector3(0.28, 0.005, 0.03), WOOD)
	return b.commit()

# ------------------------------------------------------------------ the Gothic Cathedral

const NAVE_HX := 0.085        # half the nave's width (between the arcades)
const AISLE_X := 0.13         # the outside of an aisle
const Z_FRONT := 0.285        # the west front
const Z_BACK := -0.31         # the end of the apse's straight part
const TRANS_X := 0.25
const TRANS_Z := -0.04
const TRANS_HZ := 0.07
const AISLE_H := 0.08
const NAVE_H := 0.135
const TOWER_X := 0.09
const TOWER_HW := 0.04

## One lancet window on each bay of a wall: `x` the wall's position, `dir` the way it faces, along Z from z0 to z1 in `n` bays.
static func _bays(b: M7MeshBuilder, x: float, dir: Vector3, z0: float, z1: float, n: int, y: float, w: float, h: float) -> void:
	for k in n:
		var z := z0 + (z1 - z0) * (k + 0.5) / n
		_lancet(b, Vector3(x, y, z), w, h, dir)

## A flying buttress from a pier outside the aisle up to the clerestory wall, one on each side, at z.
static func _flying(b: M7MeshBuilder, z: float) -> void:
	for side in [-1.0, 1.0]:
		var px: float = side * (AISLE_X + 0.018)
		b.box(Vector3(px, 0.05, z), Vector3(0.016, 0.1, 0.022), LIMESTONE, LIMESTONE_LIGHT)
		_pinnacle(b, Vector3(px, 0.1, z), 0.009, 0.026)
		b.pole(Vector3(px, 0.095, z), Vector3(side * NAVE_HX, NAVE_H - 0.01, z), 0.0075, LIMESTONE)
		b.pole(Vector3(px, 0.075, z), Vector3(side * (NAVE_HX + 0.012), NAVE_H - 0.032, z), 0.005, LIMESTONE_DARK)

## A square west tower with set-backs, `h` high (a flat parapet when `spire` is false): louvred belfry windows, a stair turret on one corner.
static func _tower(b: M7MeshBuilder, x: float, h: float, spire: bool, civ_banner: bool) -> void:
	var p := Vector3(x, 0, Z_FRONT - 0.03)
	var r := TOWER_HW * 1.414
	b.prism(p + Vector3(0, -0.02, 0), r * 1.12, r * 1.04, 0.02 + h * 0.3, 4, LIMESTONE_DARK, LIMESTONE_DARK, PI / 4.0)
	b.prism(p + Vector3(0, h * 0.3, 0), r * 1.04, r, h * 0.7, 4, LIMESTONE, LIMESTONE_LIGHT, PI / 4.0)
	if h < 0.1:
		return
	b.prism(p + Vector3(0, h * 0.3, 0), r * 1.08, r * 1.08, 0.006, 4, LIMESTONE_LIGHT, LIMESTONE_LIGHT, PI / 4.0)
	b.prism(p + Vector3(0, h * 0.62, 0), r * 1.08, r * 1.08, 0.006, 4, LIMESTONE_LIGHT, LIMESTONE_LIGHT, PI / 4.0)
	# a stair turret on the outer corner, buttresses at the front corners
	var out := 1.0 if x > 0.0 else -1.0
	b.prism(p + Vector3(out * r * 0.62, 0, -r * 0.62), 0.013, 0.012, h + 0.03, 6, LIMESTONE_DARK, LIMESTONE)
	for sx in [-1.0, 1.0]:
		b.box(p + Vector3(sx * TOWER_HW * 1.04, h * 0.16, TOWER_HW * 1.0), Vector3(0.012, h * 0.32, 0.014), LIMESTONE_DARK, LIMESTONE)
	# front windows and the louvred belfry openings on three faces
	_lancet(b, p + Vector3(0, h * 0.34, TOWER_HW * 1.02), 0.016, 0.036, Vector3(0, 0, 1))
	for dir in [Vector3(0, 0, 1), Vector3(1, 0, 0), Vector3(-1, 0, 0)]:
		var o: Vector3 = dir * TOWER_HW * 1.02
		var across := Vector3(-dir.z, 0.0, dir.x)
		for sgn in [-0.012, 0.012]:
			b.box(p + o + across * sgn + Vector3(0, h * 0.8, 0), Vector3(0.008 if absf(dir.x) < 0.5 else 0.004, 0.046, 0.004 if absf(dir.x) < 0.5 else 0.008), ARCH)
	if spire:
		# a corbelled parapet, then an octagonal spire with a gold finial, a pinnacle on every corner
		b.prism(p + Vector3(0, h, 0), r * 1.14, r * 1.04, 0.014, 4, LIMESTONE_LIGHT, LIMESTONE_LIGHT, PI / 4.0)
		for sx in [-1.0, 1.0]:
			for sz in [-1.0, 1.0]:
				_pinnacle(b, p + Vector3(sx * TOWER_HW * 0.94, h + 0.012, sz * TOWER_HW * 0.94), 0.007, 0.03)
		b.prism(p + Vector3(0, h + 0.014, 0), r * 0.86, 0.0, 0.17, 8, SLATE, SLATE_DARK, PI / 8.0)
		b.pole(p + Vector3(0, h + 0.18, 0), p + Vector3(0, h + 0.215, 0), 0.0026, GOLD)
		if civ_banner:
			b.tag = CIV
			var top := p + Vector3(0, h + 0.21, 0)
			b.quad(top, top + Vector3(0.062, -0.01, 0), top + Vector3(0.062, -0.036, 0), top + Vector3(0, -0.026, 0), Color.WHITE, top + Vector3(0, 0, 1.0))
			b.tag = PLAIN
	else:
		b.prism(p + Vector3(0, h, 0), r * 1.1, r * 1.1, 0.012, 4, LIMESTONE_LIGHT, LIMESTONE_LIGHT, PI / 4.0)
		for k in 8:
			var a := TAU * k / 8.0 + PI / 8.0
			b.box(p + Vector3(cos(a) * r * 0.98, h + 0.018, sin(a) * r * 0.98), Vector3(0.016, 0.014, 0.016), LIMESTONE_LIGHT)

static func _cathedral(kit, stage: int) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1160 + stage)
	b.tag = PLAIN
	var done := stage == 4
	# how far the walls have risen: stage 1 foundations only, 2 about half, 3 and 4 full
	var f: float = [0.0, 0.0, 0.5, 1.0, 1.0][stage]
	var aisle_h: float = AISLE_H * f + (0.012 if f == 0.0 else 0.0)
	var nave_h: float = NAVE_H * f + (0.012 if f == 0.0 else 0.0)
	var trans_h: float = nave_h
	# a stone platform under the whole church, stepped at the west front
	b.box(Vector3(0, 0.006, -0.01), Vector3(0.34, 0.012, 0.66), STONE_DARK, STONE)
	b.box(Vector3(0, 0.006, -0.04), Vector3(TRANS_X * 2.0 + 0.04, 0.012, TRANS_HZ * 2.0 + 0.04), STONE_DARK, STONE)
	b.box(Vector3(0, 0.003, Z_FRONT + 0.05), Vector3(0.14, 0.006, 0.05), STONE_LIGHT)
	b.box(Vector3(0, 0.009, Z_FRONT + 0.03), Vector3(0.12, 0.006, 0.04), STONE_LIGHT)
	# the aisle walls (two, outside the nave arcades), the clerestory walls, and the apse: a half-octagon closing the east end
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * (AISLE_X - 0.006), aisle_h * 0.5 + 0.008, (Z_FRONT + Z_BACK) * 0.5 + 0.03), Vector3(0.014, aisle_h, Z_FRONT - Z_BACK - 0.05), LIMESTONE, LIMESTONE_LIGHT)
		b.box(Vector3(sx * (NAVE_HX - 0.006), nave_h * 0.5 + 0.008, (Z_FRONT + Z_BACK) * 0.5 + 0.03), Vector3(0.014, nave_h, Z_FRONT - Z_BACK - 0.05), LIMESTONE, LIMESTONE_LIGHT)
	b.prism(Vector3(0, 0.008, Z_BACK + 0.02), 0.104, 0.098, aisle_h * 1.1 + 0.002, 8, LIMESTONE, LIMESTONE_LIGHT, PI / 8.0)
	# the transept: two arms across the nave, with their end walls
	b.box(Vector3(0, trans_h * 0.5 + 0.008, TRANS_Z), Vector3(TRANS_X * 2.0, trans_h, TRANS_HZ * 2.0), LIMESTONE, LIMESTONE_LIGHT)
	# the west front between the towers: the central portal and the wall of the nave, at full height only when the walls are up
	var front_h: float = NAVE_H * f + (0.012 if f == 0.0 else 0.0)
	b.box(Vector3(0, front_h * 0.5 + 0.008, Z_FRONT - 0.02), Vector3(0.12, front_h, 0.03), LIMESTONE, LIMESTONE_LIGHT)
	if f >= 1.0:
		# the portal: three deep pointed arches, and over it the rose window
		b.box(Vector3(0, 0.032, Z_FRONT - 0.002), Vector3(0.034, 0.056, 0.014), ARCH)
		b.tri(Vector3(-0.017, 0.06, Z_FRONT + 0.006), Vector3(0.017, 0.06, Z_FRONT + 0.006), Vector3(0, 0.082, Z_FRONT + 0.006), ARCH, Vector3(0, 0.05, Z_FRONT - 0.2))
		b.box(Vector3(0, 0.032, Z_FRONT - 0.001), Vector3(0.044, 0.062, 0.004), LIMESTONE_DARK)
		for sx in [-1.0, 1.0]:
			b.box(Vector3(sx * 0.044, 0.026, Z_FRONT - 0.002), Vector3(0.018, 0.044, 0.012), ARCH)
		if done:
			_rose(b, Vector3(0, 0.113, Z_FRONT - 0.0035), 0.024, Vector3(0, 0, 1))
	# the west towers: stage 1 only their footings, stage 2 about half height, 3 up with a flat parapet, 4 finished with spires
	var th: float = [0.0, 0.012, 0.15, 0.3, 0.3][stage]
	for sx in [-1.0, 1.0]:
		_tower(b, sx * TOWER_X, th, done, done)
	# bays of lancet windows along the aisle and clerestory walls once they stand
	if f >= 1.0:
		for sx in [-1.0, 1.0]:
			var dir := Vector3(sx, 0, 0)
			_bays(b, sx * AISLE_X + sx * 0.0005, dir, -0.28, 0.2, 6, 0.028, 0.016, 0.034)
			_bays(b, sx * (NAVE_HX + 0.0005), dir, -0.28, 0.2, 6, 0.09, 0.014, 0.03)
		for k in [-1.0, 0.0, 1.0]:
			var aa: float = -PI * 0.5 + k * PI * 0.25
			var adir := Vector3(cos(aa), 0.0, sin(aa))
			_lancet(b, Vector3(0, 0.028, Z_BACK + 0.02) + adir * 0.0925, 0.016, 0.036, adir)
		for sx in [-1.0, 1.0]:
			_rose(b, Vector3(sx * (TRANS_X + 0.0035), 0.09, TRANS_Z), 0.022, Vector3(sx, 0, 0))
			_lancet(b, Vector3(sx * (TRANS_X + 0.001), 0.022, TRANS_Z - 0.032), 0.016, 0.032, Vector3(sx, 0, 0))
			_lancet(b, Vector3(sx * (TRANS_X + 0.001), 0.022, TRANS_Z + 0.032), 0.016, 0.032, Vector3(sx, 0, 0))
	if done:
		# the roofs: slate over the nave (ridge along Z) and the transept (ridge along X), aisles under lean-to slopes, a conical roof on the apse
		_roof_z(b, 0.0, NAVE_HX + 0.01, Z_BACK + 0.03, Z_FRONT - 0.02, NAVE_H + 0.008, 0.062, SLATE, SLATE_DARK)
		_roof_x(b, TRANS_Z, TRANS_HZ + 0.008, -TRANS_X - 0.004, TRANS_X + 0.004, NAVE_H + 0.008, 0.058, SLATE, LIMESTONE)
		for sx in [-1.0, 1.0]:
			var z0 := Z_BACK + 0.03
			var z1 := Z_FRONT - 0.03
			b.quad(Vector3(sx * AISLE_X, AISLE_H + 0.008, z0), Vector3(sx * AISLE_X, AISLE_H + 0.008, z1), Vector3(sx * (NAVE_HX - 0.002), AISLE_H + 0.03, z1),
				Vector3(sx * (NAVE_HX - 0.002), AISLE_H + 0.03, z0), SLATE if sx > 0.0 else SLATE_DARK, Vector3(sx * 0.11, 0.0, (z0 + z1) * 0.5))
		b.prism(Vector3(0, aisle_h * 1.1 + 0.008, Z_BACK + 0.02), 0.11, 0.0, 0.07, 8, SLATE, SLATE_DARK, PI / 8.0)
		# the crossing: a lantern with louvres and a tall slender spire on it, gold at the tip
		var cc := Vector3(0, NAVE_H + 0.05, TRANS_Z)
		b.prism(cc + Vector3(0, 0.0, 0), 0.032, 0.03, 0.058, 8, LIMESTONE, LIMESTONE_LIGHT, PI / 8.0)
		for k in 8:
			var a := TAU * k / 8.0 + PI / 8.0
			b.box(cc + Vector3(cos(a) * 0.03, 0.03, sin(a) * 0.03), Vector3(0.006, 0.03, 0.006), ARCH)
		b.prism(cc + Vector3(0, 0.058, 0), 0.036, 0.034, 0.006, 8, LIMESTONE_LIGHT, LIMESTONE_LIGHT, PI / 8.0)
		b.prism(cc + Vector3(0, 0.064, 0), 0.032, 0.0, 0.3, 8, SLATE, SLATE_DARK, PI / 8.0)
		b.pole(cc + Vector3(0, 0.36, 0), cc + Vector3(0, 0.395, 0), 0.0026, GOLD)
		# flying buttresses along the nave on both sides, with a pinnacle on each pier and on the transept's corners and the front gable
		for z in [-0.24, -0.15, 0.1, 0.19]:
			_flying(b, z)
		for sx in [-1.0, 1.0]:
			_pinnacle(b, Vector3(sx * (TRANS_X - 0.01), NAVE_H, TRANS_Z - TRANS_HZ + 0.008), 0.009, 0.03)
			_pinnacle(b, Vector3(sx * (TRANS_X - 0.01), NAVE_H, TRANS_Z + TRANS_HZ - 0.008), 0.009, 0.03)
		# the owner's banner hung over the portal and a gold cross on the front gable
		b.tag = CIV
		var bp := Vector3(0, 0.132, Z_FRONT + 0.0)
		b.quad(bp + Vector3(-0.012, 0.0, 0.0), bp + Vector3(0.012, 0.0, 0.0), bp + Vector3(0.01, -0.03, 0.002), bp + Vector3(-0.01, -0.03, 0.002), Color.WHITE, bp + Vector3(0, -0.02, -1.0))
		b.tag = PLAIN
	elif stage == 3:
		# the roof still open: bare rafters over the nave, a lifting crane on the crossing
		for k in 8:
			var z := Z_BACK + 0.05 + 0.065 * k
			b.pole(Vector3(-NAVE_HX - 0.006, NAVE_H + 0.008, z), Vector3(0, NAVE_H + 0.068, z), 0.0034, WOOD_LIGHT)
			b.pole(Vector3(NAVE_HX + 0.006, NAVE_H + 0.008, z), Vector3(0, NAVE_H + 0.068, z), 0.0034, WOOD_LIGHT)
		b.pole(Vector3(0, NAVE_H + 0.068, Z_BACK + 0.05), Vector3(0, NAVE_H + 0.068, Z_FRONT - 0.04), 0.0036, WOOD)
	# scaffolding on the front and a masons' lodge with a hoist while it is being built, stacks of cut stone about
	if not done:
		for sx in [-0.14, -0.05, 0.05, 0.14]:
			b.pole(Vector3(sx, -0.01, Z_FRONT + 0.034), Vector3(sx, 0.07 + 0.09 * f, Z_FRONT + 0.034), 0.0034, WOOD_LIGHT)
		for y in [0.04, 0.08, 0.12, 0.16]:
			if y < 0.07 + 0.09 * f:
				b.box(Vector3(0, y, Z_FRONT + 0.034), Vector3(0.32, 0.005, 0.03), WOOD)
		# the masons' lodge: a long low timber shed with a plank roof beside the nave, a hoist beside it
		b.box(Vector3(-0.22, 0.03, 0.18), Vector3(0.08, 0.05, 0.14), WOOD_LIGHT, WOOD_LIGHT.lightened(0.08))
		_roof_z(b, -0.22, 0.052, 0.1, 0.26, 0.055, 0.03, WOOD)
		b.pole(Vector3(0.22, 0.0, 0.1), Vector3(0.22, 0.22, 0.1), 0.0045, WOOD)
		b.pole(Vector3(0.22, 0.2, 0.1), Vector3(0.14, 0.23, 0.1), 0.0034, WOOD_LIGHT)
		b.pole(Vector3(0.145, 0.228, 0.1), Vector3(0.145, 0.15, 0.1), 0.0013, Color("#b9a68a"))
		b.box(Vector3(0.145, 0.142, 0.1), Vector3(0.018, 0.016, 0.018), STONE_LIGHT)
		for k in 6:
			b.blob(Vector3(0.18 + 0.03 * (k % 3), 0.02 + 0.016 * (k / 3), 0.2 + 0.02 * (k / 3)), Vector3(0.022, 0.016, 0.018), 5, 3, LIMESTONE_LIGHT, 0.12, LIMESTONE)
	return b.commit()
