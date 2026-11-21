## Discovery (E6) war: the bastion fort (DEF-10 Star Forts), docs/MVP/39 §4.5.
##
## Not wired to the simulation yet (docs/MVP/39 §9: `discWorks`, `garrisoned` and the integrity already in the Iron frame's `sieges`); photographed in the
## Discovery gallery (LowPoly3DView.show_discovery_gallery). Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.1 tall). A whole-hex
## work on the Citadel hex (docs/MVP/31a, decided in docs/MVP/39 §12): it replaces the castle mesh and shares the slot with the concentric castle. The gate
## faces +Z. A star fort is LOW and WIDE where a castle is tall: a five-pointed ring of arrowhead bastions in brick and earth round a parade ground, a ditch,
## a keep and barracks within. TAG_CIV faces take the owner's colour (the banner and the gunners' coats).
##
##   bastion_1   the site: a mud ditch ring, the stone footing of the trace and a few cut blocks and stakes
##   bastion_2   the rampart half raised, timber hoardings along it and a gin crane, a barracks foundation inside
##   bastion_3   the rampart complete and turfed, no guns yet, the keep and barracks half built
##   bastion_4   finished and garrisoned: a gun on each point and a sentry box, gunners on the points, a keep with the owner's banner raised, a barracks, a
##               gatehouse at the south notch with a drawbridge over the ditch
##   bastion_4_idle  the same with no garrison: the banner lowered, no gunners (doc 39 §4.5: the defence applies only to a garrisoned fort)
##   bastion_cracked  integrity 40% to 70% (docs/MVP/39 §4.5): the scarp scarred with dark cracks and a few bricks fallen at the foot of the back bastion's face,
##                    its sentry box gone; the garrison still on the walls
##   bastion_breach   15% to 40%: the back bastion's rampart slumped to a fraction of its height, a ramp of fallen earth and brick running down into the ditch,
##                    the gun on that point thrown down, only a few gunners left; the keep and the banner still stand
##   bastion_ruin     under 15%, and after a storm: three bastions slumped to stumps, rubble across the ditch, the keep a broken stump with no roof, the barracks
##                    roofless, no banner and no gunners
##                    All three damage pieces break the bastion at angle 270 degrees (the back, -Z, opposite the gate); the view turns the whole mesh about Y so the
##                    break faces the besieger (the integrity and the besieger's origin come from the frame's `sieges`, docs/MVP/39 §9). A fort shows fewer stages
##                    than an E5 wall at the same cannon fire, because it halves the gunpowder penalty (doc 38).
extends RefCounted

const BRICK := Color("#9a4a34")
const BRICK_LIGHT := Color("#b45c42")
const BRICK_DARK := Color("#74382a")
const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const EARTH := Color("#6f8a3e")
const EARTH_LIGHT := Color("#8ea548")
const EARTH_DARK := Color("#5a4a34")
const DITCH := Color("#5a4a34")
const DITCH_DARK := Color("#43372a")
const SAND := Color("#cdb98a")
const TIMBER := Color("#6b5038")
const TIMBER_DARK := Color("#4a3726")
const TIMBER_LIGHT := Color("#8a6a48")
const SLATE := Color("#4f5560")
const TILE := Color("#a8492c")
const TILE_DARK := Color("#7f3622")
const CAST := Color("#4d5057")
const CAST_LIGHT := Color("#72777f")
const ROPE := Color("#b9a68a")
const SKIN := Color("#d9b48f")
const COAL_DARK := Color("#17171a")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV

const POINTS := 5
const RN := 0.225     # the notch radius (the re-entrant angle between two bastions)
const RS := 0.31      # the shoulder radius
const RT := 0.345     # the tip radius
const H := 0.07       # the finished rampart height
const GATE_ANGLE := PI * 0.5   # the south notch faces +Z

static func build(kit) -> void:
	kit.meshes["bastion_1"] = _fort(kit, 1, false)
	kit.meshes["bastion_2"] = _fort(kit, 2, false)
	kit.meshes["bastion_3"] = _fort(kit, 3, false)
	kit.meshes["bastion_4"] = _fort(kit, 4, true)
	kit.meshes["bastion_4_idle"] = _fort(kit, 4, false)
	kit.meshes["bastion_cracked"] = _fort(kit, 4, true, 1)
	kit.meshes["bastion_breach"] = _fort(kit, 4, true, 2)
	kit.meshes["bastion_ruin"] = _fort(kit, 4, false, 3)

## The trace of the fort: per bastion a notch, a shoulder, the tip and the other shoulder (20 points for five bastions), scaled by `k` about the centre, at height y.
static func _ring(k: float, y: float, dmg: int = 0) -> Array:
	var out := []
	for i in POINTS:
		var n := GATE_ANGLE + TAU * i / POINTS
		var part_i := 0
		for part in [[0.0, RN], [0.38, RS], [0.5, RT], [0.62, RS]]:
			var a: float = n + float(part[0]) * TAU / POINTS
			var r: float = float(part[1]) * k
			var yy := y
			if y > 0.02:
				yy = y * _slump(i, part_i, dmg)
			out.append(Vector3(cos(a) * r, yy, sin(a) * r))
			part_i += 1
	return out

## The lowest fraction of its height any part of the rampart keeps at damage level `dmg`: the inner slope and the parade ground drop to it, so a slumped rampart never
## ends up lower than the ground it slopes down to (the slope would run uphill and leave a lip).
static func _slump_floor(dmg: int) -> float:
	return 0.08 if dmg >= 2 else 1.0

## How much of its height the rampart keeps at vertex `part` of bastion `i` for damage level `dmg` (0 whole .. 3 ruined). The back bastion (i = 2, angle 270
## degrees) takes the damage first: cracked loses a little at its tip, breached loses most of the tip and shoulders, ruined leaves three bastions as stumps.
static func _slump(i: int, part: int, dmg: int) -> float:
	if dmg == 1:
		return 0.82 if (i == 2 and part == 2) else 1.0
	if dmg == 2:
		if i == 2:
			return 0.1 if part == 2 else (0.4 if (part == 1 or part == 3) else 0.85)
		if i == 3 and part == 0:
			return 0.85
		return 1.0
	if dmg == 3:
		if i == 2:
			return 0.08 if part != 0 else 0.3
		if i == 3:
			return 0.45 if part == 0 else (0.12 if part == 1 else (0.5 if part == 2 else 0.8))
		if i == 1:
			return 0.55 if part == 3 else (0.4 if part == 2 else 0.8)
		return 0.9
	return 1.0

## A band of quads joining two rings; `mode` 0 faces outward, 1 up, 2 inward (toward the parade ground).
static func _band(b: M7MeshBuilder, ra: Array, rb: Array, col: Color, mode: int, alt: Color = Color.TRANSPARENT) -> void:
	var n := ra.size()
	for i in n:
		var j := (i + 1) % n
		var a: Vector3 = ra[i]
		var bb: Vector3 = ra[j]
		var c: Vector3 = rb[j]
		var d: Vector3 = rb[i]
		var cen := (a + bb + c + d) * 0.25
		var hint := Vector3(0, cen.y, 0)
		if mode == 1:
			hint = Vector3(cen.x, cen.y - 1.0, cen.z)
		elif mode == 2:
			hint = Vector3(cen.x * 4.0, cen.y, cen.z * 4.0)
		var col_i: Color = col
		if alt.a > 0.0 and i % 2 == 1:
			col_i = alt
		b.quad(a, bb, c, d, col_i, hint)

static func _fort(kit, stage: int, garrisoned: bool, dmg: int = 0) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1800 + stage + 10 * dmg)
	b.tag = PLAIN
	var h := H * (0.42 if stage == 2 else 1.0)
	if stage == 1:
		h = 0.012
	# the ditch: a ring of bare earth round the trace, darker at its foot, and a worn glacis outside it
	_band(b, _ring(1.2, 0.0015), _ring(1.1, 0.0015), DITCH, 1, DITCH_DARK)
	_band(b, _ring(1.28, 0.0), _ring(1.2, 0.0015), EARTH, 1, EARTH_LIGHT)
	# the scarp (the brick facing), a stone cordon, the sloped earth parapet, the terreplein and the inner slope
	_band(b, _ring(1.08, 0.0), _ring(1.045, h * 0.62, dmg), BRICK, 0, BRICK_DARK)
	_band(b, _ring(1.045, h * 0.62, dmg), _ring(1.03, h * 0.7, dmg), STONE_LIGHT, 0)
	_band(b, _ring(1.03, h * 0.7, dmg), _ring(0.985, h, dmg), EARTH, 0)
	_band(b, _ring(0.985, h, dmg), _ring(0.88, h, dmg), EARTH_LIGHT, 1, EARTH)
	var floor_y := minf(0.012, h * _slump_floor(dmg))
	_band(b, _ring(0.88, h, dmg), _ring(0.86, floor_y), EARTH_DARK, 2)
	# the parade ground: a sandy floor over the middle
	var floor_ring := _ring(0.86, floor_y)
	for i in floor_ring.size():
		var j := (i + 1) % floor_ring.size()
		b.tri(Vector3(0, floor_y, 0), floor_ring[i], floor_ring[j], SAND, Vector3(0, -1, 0))
	if stage == 1:
		_stage1(b)
	elif stage == 2:
		_stage2(b)
	elif stage == 3:
		_stage3(b)
	else:
		_stage4(b, h, garrisoned, dmg)
		if dmg > 0:
			_damage(b, h, dmg)
	return b.commit()

static func _tip(k: float, i: int) -> Vector3:
	var a := GATE_ANGLE + TAU * i / POINTS + 0.5 * TAU / POINTS
	return Vector3(cos(a) * RT * k, 0.0, sin(a) * RT * k)

static func _block(b: M7MeshBuilder, p: Vector3, s: Vector3, col: Color) -> void:
	b.box(p + Vector3(0, s.y * 0.5, 0), s, col, col.lightened(0.08))

static func _stage1(b: M7MeshBuilder) -> void:
	# the stone footing: a low ring of cut stone along the line of the scarp, and a few blocks stacked by the way in
	var fr := _ring(1.08, 0.0)
	for i in fr.size():
		var j := (i + 1) % fr.size()
		var p: Vector3 = fr[i]
		var q: Vector3 = fr[j]
		b.pole(p + Vector3(0, 0.006, 0), q + Vector3(0, 0.006, 0), 0.007, STONE if i % 2 == 0 else STONE_DARK, 4)
	for k in 3:
		_block(b, Vector3(0.0 + 0.032 * k, 0.012, 0.28), Vector3(0.026, 0.014, 0.018), STONE_LIGHT)
	_block(b, Vector3(0.016, 0.026, 0.28), Vector3(0.026, 0.014, 0.018), STONE)
	# survey stakes and a line of rope marking the inner line, a hut for the engineers
	for i in 5:
		var t := _tip(0.9, i)
		b.pole(t, t + Vector3(0, 0.03, 0), 0.0025, TIMBER_LIGHT, 3)
	b.box(Vector3(-0.12, 0.014, 0.1), Vector3(0.05, 0.028, 0.035), TIMBER, TIMBER_LIGHT)
	b.pole(Vector3(-0.095, 0.028, 0.1), Vector3(-0.145, 0.028, 0.1), 0.004, TIMBER_DARK, 4)

static func _stage2(b: M7MeshBuilder) -> void:
	# timber hoardings and ladders along the half-raised rampart, and a gin crane at the south notch lifting a block
	for i in POINTS:
		var t := _tip(0.96, i)
		b.pole(t + Vector3(0, 0.03, 0), t + Vector3(0, 0.1, 0), 0.0032, TIMBER, 4)
		b.pole(t * 0.8 + Vector3(0, 0.03, 0), t * 0.8 + Vector3(0, 0.09, 0), 0.0032, TIMBER, 4)
		b.pole(t + Vector3(0, 0.1, 0), t * 0.8 + Vector3(0, 0.09, 0), 0.0028, TIMBER_LIGHT, 4)
	var g := Vector3(0.0, 0.03, 0.2)
	b.pole(g, g + Vector3(0.0, 0.11, -0.03), 0.0045, TIMBER, 4)
	b.pole(g + Vector3(0.03, 0, 0.02), g + Vector3(0.0, 0.11, -0.03), 0.0035, TIMBER_DARK, 4)
	b.pole(g + Vector3(-0.03, 0, 0.02), g + Vector3(0.0, 0.11, -0.03), 0.0035, TIMBER_DARK, 4)
	b.pole(g + Vector3(0.0, 0.11, -0.03), g + Vector3(0.0, 0.055, 0.045), 0.0015, ROPE, 3)
	_block(b, g + Vector3(0.0, 0.035, 0.045), Vector3(0.018, 0.016, 0.014), STONE_LIGHT)
	# a barracks foundation inside and a pile of cut stone and fascines
	b.box(Vector3(-0.02, 0.018, -0.06), Vector3(0.2, 0.012, 0.07), STONE_DARK, STONE)
	for k in 4:
		b.pole(Vector3(0.12 + 0.006 * k, 0.02, 0.0), Vector3(0.12 + 0.006 * k, 0.02, 0.1), 0.0045, TIMBER_LIGHT, 4)

static func _stage3(b: M7MeshBuilder) -> void:
	# the turfed rampart is whole; inside, the barracks walls stand without a roof and the keep is a stump
	b.box(Vector3(-0.02, 0.036, -0.06), Vector3(0.2, 0.036, 0.07), BRICK, BRICK_LIGHT)
	b.prism(Vector3(0.0, 0.012, 0.0), 0.045, 0.04, 0.06, 8, STONE, STONE_LIGHT, PI / 8.0)
	b.pole(Vector3(0.0, 0.07, 0.0), Vector3(0.0, 0.17, 0.0), 0.003, TIMBER, 4)
	for t in 3:
		b.pole(Vector3(0.04, 0.012, 0.0), Vector3(0.0, 0.06 + 0.02 * t, 0.0), 0.0025, TIMBER_DARK, 3)
	# the gun platforms laid, the carriages not yet brought: a timber deck on each point
	for i in POINTS:
		var t := _tip(0.8, i)
		b.prism(t + Vector3(0, H, 0), 0.03, 0.03, 0.004, 8, TIMBER_LIGHT, TIMBER_LIGHT.lightened(0.1))

static func _gun(b: M7MeshBuilder, at: Vector3, dir: Vector3) -> void:
	# a gun on a deck carriage pointing along dir: a short iron barrel, a block carriage with two wheels
	var d := Vector3(dir.x, 0, dir.z).normalized()
	var side := Vector3(-d.z, 0, d.x)
	b.box(at + Vector3(0, 0.008, 0), Vector3(0.016, 0.01, 0.016), TIMBER_DARK)
	b.pole(at + Vector3(0, 0.016, 0) - d * 0.014, at + Vector3(0, 0.02, 0) + d * 0.026, 0.0062, CAST, 6)
	b.blob(at + Vector3(0, 0.0165, 0) - d * 0.016, Vector3(0.0065, 0.0065, 0.0065), 5, 2, CAST_LIGHT, 0.0)
	for sd in [-1.0, 1.0]:
		var s: float = sd
		b.prism(at + side * s * 0.01 + Vector3(0, 0.0, 0), 0.007, 0.007, 0.003, 8, TIMBER, TIMBER_LIGHT)

static func _figure(b: M7MeshBuilder, p: Vector3) -> void:
	# a gunner a tenth tall: a coat in the owner's colour, a hat and a pale face
	var save := b.tag
	b.tag = CIV
	b.prism(p, 0.0055, 0.0045, 0.02, 5, Color.WHITE, Color.WHITE)
	b.tag = PLAIN
	b.blob(p + Vector3(0, 0.024, 0), Vector3(0.0045, 0.0045, 0.0045), 4, 2, SKIN, 0.0)
	b.prism(p + Vector3(0, 0.0265, 0), 0.0058, 0.0036, 0.004, 5, TIMBER_DARK, TIMBER_DARK)
	b.tag = save

static func _stage4(b: M7MeshBuilder, h: float, garrisoned: bool, dmg: int = 0) -> void:
	# the keep: an eight-sided stone tower with a tiled cone and a flagpole; the barracks: a long brick block with a tiled gable roof
	if dmg < 3:
		b.prism(Vector3(0.0, 0.012, 0.0), 0.05, 0.044, 0.1, 8, STONE, STONE_LIGHT, PI / 8.0)
		b.prism(Vector3(0.0, 0.112, 0.0), 0.054, 0.0, 0.05, 8, TILE, TILE_DARK, PI / 8.0)
		b.box(Vector3(0.0, 0.062, 0.0455), Vector3(0.018, 0.028, 0.006), COAL_DARK)
		for k in 4:
			var a := TAU * k / 4.0 + PI * 0.25
			b.box(Vector3(cos(a) * 0.0455, 0.092, sin(a) * 0.0455), Vector3(0.006, 0.014, 0.006), COAL_DARK)
		b.box(Vector3(-0.02, 0.0475, -0.075), Vector3(0.2, 0.07, 0.07), BRICK, BRICK_LIGHT)
		_gable_x(b, Vector3(-0.02, 0.0825, -0.075), 0.2, 0.07, 0.04, TILE, TILE_DARK, BRICK_DARK)
		for k in 5:
			b.box(Vector3(-0.1 + 0.04 * k, 0.05, -0.0395), Vector3(0.012, 0.02, 0.003), COAL_DARK)
		# the banner on the keep: raised when garrisoned, lowered to half the pole when not
		b.pole(Vector3(0.0, 0.158, 0.0), Vector3(0.0, 0.25, 0.0), 0.0026, TIMBER, 4)
		var fy := 0.236 if garrisoned else 0.17
		b.tag = CIV
		b.quad(Vector3(0.0, fy, 0.0), Vector3(0.056, fy - 0.006, 0.0), Vector3(0.056, fy - 0.034, 0.0), Vector3(0.0, fy - 0.03, 0.0), Color.WHITE, Vector3(0.03, fy - 0.02, 1.0))
		b.quad(Vector3(0.0, fy, 0.0), Vector3(0.056, fy - 0.006, 0.0), Vector3(0.056, fy - 0.034, 0.0), Vector3(0.0, fy - 0.03, 0.0), Color.WHITE, Vector3(0.03, fy - 0.02, -1.0))
		b.tag = PLAIN
	else:
		# a ruin: the keep a broken stump with a jagged top and no roof; the barracks' walls standing roofless with a few charred rafters; the pole snapped
		b.prism(Vector3(0.0, 0.012, 0.0), 0.05, 0.046, 0.05, 8, STONE, STONE_LIGHT, PI / 8.0)
		for k in 5:
			var ka := TAU * k / 5.0
			b.box(Vector3(cos(ka) * 0.034, 0.066 + 0.012 * (k % 3), sin(ka) * 0.034), Vector3(0.016, 0.016 + 0.01 * (k % 2), 0.016), STONE_DARK, STONE)
		b.box(Vector3(-0.02, 0.0375, -0.075), Vector3(0.2, 0.05, 0.07), BRICK_DARK, BRICK)
		for k in 4:
			b.pole(Vector3(-0.1 + 0.07 * k, 0.065, -0.095), Vector3(-0.09 + 0.07 * k, 0.1 + 0.01 * (k % 2), -0.06), 0.0028, Color("#2a2119"), 4)
		b.pole(Vector3(0.0, 0.058, 0.0), Vector3(0.012, 0.09, 0.006), 0.0026, TIMBER_DARK, 4)
	# a gun and a sentry box (a small stone cabin with a cone cap) on every point, and gunners when garrisoned
	for i in POINTS:
		var tp := _tip(0.8, i)
		var out := Vector3(tp.x, 0, tp.z).normalized()
		# which bastions are broken: the back one (i = 2) from a breach on, three of them in a ruin
		var broken := (dmg >= 2 and i == 2) or (dmg >= 3 and (i == 1 or i == 3))
		var scarred := dmg >= 1 and i == 2
		if not broken:
			_gun(b, Vector3(tp.x, h, tp.z), out)
		if not scarred and not broken:
			var sb := _tip(0.93, i)
			b.box(Vector3(sb.x, h + 0.012, sb.z), Vector3(0.016, 0.024, 0.016), STONE, STONE_LIGHT)
			b.prism(Vector3(sb.x, h + 0.024, sb.z), 0.013, 0.0, 0.014, 6, TILE, TILE_DARK)
		if garrisoned and not broken:
			_figure(b, Vector3(tp.x - out.z * 0.022, h, tp.z + out.x * 0.022))
			if not scarred or dmg == 1:
				_figure(b, Vector3(tp.x + out.z * 0.024 - out.x * 0.01, h, tp.z - out.x * 0.024 - out.z * 0.01))
	# along the curtains: a few more gunners on the south faces
	if garrisoned:
		for i in (5 if dmg < 2 else 2):
			var a := GATE_ANGLE + (float(i) - 2.0) * 0.16
			_figure(b, Vector3(cos(a) * 0.2, h, sin(a) * 0.2))
	# the gatehouse at the south notch: a brick tower with a dark arch, and the drawbridge laid across the ditch
	var gp := Vector3(0.0, 0.0, RN * 1.0)
	b.box(gp + Vector3(0, 0.055, -0.012), Vector3(0.06, 0.11, 0.04), BRICK, BRICK_LIGHT)
	b.box(gp + Vector3(0, 0.03, 0.0095), Vector3(0.026, 0.056, 0.004), COAL_DARK)
	b.prism(gp + Vector3(0, 0.11, -0.012), 0.044, 0.0, 0.03, 4, TILE, TILE_DARK, PI * 0.25)
	for k in 6:
		b.box(gp + Vector3(-0.012 + 0.0048 * k, 0.003, 0.07), Vector3(0.0042, 0.004, 0.11), TIMBER_LIGHT if k % 2 == 0 else TIMBER)
	b.pole(gp + Vector3(-0.016, 0.07, 0.02), gp + Vector3(-0.016, 0.01, 0.12), 0.0014, ROPE, 3)
	b.pole(gp + Vector3(0.016, 0.07, 0.02), gp + Vector3(0.016, 0.01, 0.12), 0.0014, ROPE, 3)

## A gable roof with the ridge along X (as art_disc_power's, kept local so each family file stands alone).
static func _gable_x(b: M7MeshBuilder, c: Vector3, w: float, d: float, h: float, col: Color, col_back: Color, col_end: Color) -> void:
	var x0 := c.x - w * 0.5 - 0.005
	var x1 := c.x + w * 0.5 + 0.005
	var zf := c.z + d * 0.5 + 0.006
	var zb := c.z - d * 0.5 - 0.006
	var below := Vector3(c.x, c.y - 0.5, c.z)
	b.quad(Vector3(x0, c.y, zf), Vector3(x1, c.y, zf), Vector3(x1, c.y + h, c.z), Vector3(x0, c.y + h, c.z), col, below)
	b.quad(Vector3(x0, c.y, zb), Vector3(x1, c.y, zb), Vector3(x1, c.y + h, c.z), Vector3(x0, c.y + h, c.z), col_back, below)
	b.tri(Vector3(x0 + 0.005, c.y, zf - 0.006), Vector3(x0 + 0.005, c.y, zb + 0.006), Vector3(x0 + 0.005, c.y + h, c.z), col_end, Vector3(c.x, c.y + h * 0.4, c.z))
	b.tri(Vector3(x1 - 0.005, c.y, zf - 0.006), Vector3(x1 - 0.005, c.y, zb + 0.006), Vector3(x1 - 0.005, c.y + h, c.z), col_end, Vector3(c.x, c.y + h * 0.4, c.z))

## The damage itself, on top of the slumped rampart: cracks and fallen bricks for a cracked fort; a ramp of rubble running down into the ditch and a gun thrown
## down for a breach; rubble across the ditch at every broken bastion for a ruin. The back bastion (angle 270 degrees) is the one the besieger hits first.
static func _damage(b: M7MeshBuilder, h: float, dmg: int) -> void:
	var back := PI * 1.5
	if dmg == 1:
		# the scarp scarred with dark cracks round the back tip, a few bricks come away and lying at its foot
		for k in 4:
			var a := back + (float(k) - 1.5) * 0.045
			var p0 := Vector3(cos(a) * RT * 1.075, 0.008, sin(a) * RT * 1.075)
			var p1 := Vector3(cos(a + 0.012) * RT * 1.05, 0.03 + 0.006 * (k % 2), sin(a + 0.012) * RT * 1.05)
			b.pole(p0, p1, 0.0021, COAL_DARK, 3)
		for k in 5:
			var ba := back + (float(k) - 2.0) * 0.07
			var r := RT * (1.12 + 0.02 * (k % 2))
			b.box(Vector3(cos(ba) * r, 0.0035, sin(ba) * r), Vector3(0.011, 0.007, 0.007), BRICK if k % 2 == 0 else BRICK_DARK)
		return
	# a breach or a ruin: a ramp of spilt earth and brick from the slumped rampart down to the ditch floor, and lumps all about it
	var spread := 0.07 if dmg == 2 else 0.1
	var tip := Vector3(0, 0, -RT)
	var lo := Vector3(0, 0.004, -RT * 1.3)
	var hi := Vector3(0, h * 0.34, -RT * 0.78)
	var w := spread * 0.9
	b.quad(hi + Vector3(-w * 0.6, 0, 0), hi + Vector3(w * 0.6, 0, 0), lo + Vector3(w * 1.2, 0, 0), lo + Vector3(-w * 1.2, 0, 0), EARTH_DARK, Vector3(0, -1, 0))
	for k in (9 if dmg == 2 else 14):
		var u := (float(k) + 0.5) / (9.0 if dmg == 2 else 14.0)
		var x := (b.rnd() - 0.5) * 2.0 * spread * (0.4 + u)
		var z := lerpf(-RT * 0.8, -RT * 1.28, u) + (b.rnd() - 0.5) * 0.02
		var col: Color = BRICK if k % 3 == 0 else (BRICK_DARK if k % 3 == 1 else EARTH_DARK)
		var sz := 0.012 + 0.012 * b.rnd()
		b.blob(Vector3(x, 0.006 + sz * 0.3 + (1.0 - u) * h * 0.2, z), Vector3(sz, sz * 0.7, sz), 4, 2, col, 0.2, col.darkened(0.2))
	# a gun thrown down the slope: the barrel on its side, a wheel off
	b.pole(Vector3(-0.025, 0.014, -RT * 1.05), Vector3(0.02, 0.01, -RT * 1.12), 0.0062, CAST, 6)
	b.prism(Vector3(0.035, 0.0035, -RT * 1.0), 0.007, 0.007, 0.003, 8, TIMBER, TIMBER_LIGHT)
	if dmg == 3:
		# the other broken bastions: rubble heaps across the ditch at the shoulders
		for sgn in [-1.0, 1.0]:
			var ang: float = back + sgn * TAU / POINTS
			for k in 7:
				var r2 := RT * (0.8 + 0.1 * k)
				var off := (float(k % 3) - 1.0) * 0.03
				b.blob(Vector3(cos(ang) * r2 - sin(ang) * off, 0.008, sin(ang) * r2 + cos(ang) * off), Vector3(0.016 + 0.004 * (k % 2), 0.012, 0.016), 4, 2, BRICK_DARK if k % 2 == 0 else EARTH_DARK, 0.2)
