## Worked deposits: what a deposit looks like once someone digs it, and once it is dug out. world_builder.gd `_deposits()` puts one
## IN PLACE OF the untouched marker (art_deposits.gd) on a tile whose deposit has produced ore (e3 overlay byte 1, bit 0), at the
## marker's spot and turn, so the two never overlap; `M7WorldBuilder.mine_mesh_name` picks the mesh:
##   deposit_mine<kind>              the working of the kind's family
##   deposit_mine<kind>_head         the headframe (copper ore, arsenical copper, iron when deep or under a camp mine head; tin lode)
##   deposit_mine<kind>[_head]_spent the same, exhausted (byte 1 bit 3)
## each with a `_tall` twin for mountain tiles.
##
## A live working keeps the marker's pole and glowing gem (the kind still reads from afar) and adds a small pennant in the colour of the
## civilization working it (TAG_CIV: INSTANCE_CUSTOM.rgb). Around the pole the untouched outcrop gives way to one of four workings,
## tinted per kind from art_deposits.gd KINDS (spoil in the outcrop colours, the ore it yields in the face colour):
##   adit      copper ore, arsenical copper, iron: a rock face cut by a timber-framed adit with a fire-setting glow inside, a spoil
##             heap, baskets of picked ore and a maul
##   placer    tin placer, gold, silver: a washing channel through gravel banks, a riffled sluice on trestles, washed gravel heaps, a
##             pan and the dressed concentrate
##   saltworks salt: a grid of clay-rimmed evaporation pans from brine to white crust, raked white heaps, a thatched drying shelter
##   headframe a tall braced timber tower with a sheave wheel over a timbered shaft collar, a windlass, a full bucket on the rope, a
##             big stained spoil heap and a lamp
## A spent working is abandoned: grass creeping back over the dug ground, the kind's colours greyed, no fire, lamp or pennant, and the
## pole leaning with a dull unlit gem. The adit is caved in and boarded across, the headframe has fallen across its boarded shaft, the
## placer's channel is dry, its sluice down and its heaps slumped, the salt pans are dry and cracked with the shelter's roof on the ground.
## Origin is the ground centre, like the marker; the pole stands at art_deposits `_pin`'s foot (0.11, 0, -0.06), so every working is
## laid out clear of it. Footprint radius about 0.2 before the 1.3 placement scale.
extends RefCounted

const DEP := preload("res://Renderer/LowPoly3D/Scripts/art_deposits.gd")

const ADIT_KINDS := [6, 7, 11]           # copper ore, arsenical copper, iron
const PLACER_KINDS := [8, 9, 10]          # tin placer, gold, silver
const SALT_KINDS := [12]
const HEAD_KINDS := [6, 7, 8, 11]         # deep or shafted copper, arsenical copper and iron, and tin lode; gold and silver are panned

const TIMBER := Color("#7a5a3a")
const TIMBER_DARK := Color("#5a4129")
const DARK := Color("#17120e")
const EARTH := Color("#7b6247")
const BASKET := Color("#b48c4e")
const ROPE := Color("#c9b48a")
const CLAY := Color("#a8704a")
const THATCH := Color("#c29a52")
const THATCH_DARK := Color("#a07c3e")
const WATER := Color("#4f86a0")
const WATER_HI := Color("#7fb2c8")
const EMBER := Color("#ff6a1a")
const SPOIL := Color("#9a9184")          # broken waste rock, paler than the living rock it came from
const LAMP := Color("#ffb648")
const REGROWTH := Color("#76884a")       # grass taking an abandoned working back
const WEATHERED := Color("#8a857c")      # the grey everything left out in the weather turns
const OLD_TIMBER := Color("#6e6150")

# what the dressed ore looks like, where it differs from the marker's face colour: tin is won as dark cassiterite
const ORE_OVERRIDE := {8: "#3d3430"}

static func build(kit) -> void:
	for kind in DEP.KINDS:
		var k := int(kind)
		var family := ""
		if k in ADIT_KINDS:
			family = "adit"
		elif k in PLACER_KINDS:
			family = "placer"
		elif k in SALT_KINDS:
			family = "salt"
		else:
			continue
		for tall in [false, true]:
			for spent in [false, true]:
				_mine(kit, k, family, tall, spent)
				if k in HEAD_KINDS:
					_mine(kit, k, "head", tall, spent)

static func _mine(kit, kind: int, family: String, tall: bool, spent: bool) -> void:
	var spec: Array = DEP.KINDS[kind]
	var base := Color(spec[0])
	var face := Color(spec[1])
	var ore := Color(ORE_OVERRIDE.get(kind, spec[1]))
	var b: M7MeshBuilder = kit._b(1700 + kind * 17 + (5 if family == "head" else 0) + (3 if spent else 0))
	var h := DEP.PIN_H_TALL if tall else DEP.PIN_H
	# trampled, dug-over ground: wider than the marker's stain and earth-brown, so a working reads as a place where people go
	var ground := EARTH.lerp(base, 0.35)
	if spent:
		ground = ground.lerp(REGROWTH, 0.45)
	b.prism(Vector3(0, -0.006, 0), 0.215, 0.2, 0.01, 10, ground.darkened(0.15), ground, b.rnd() * TAU, 0.08, true)
	if spent:
		# everything keeps its kind, but greyed by the weather
		base = _mute(base, 0.35)
		face = _mute(face, 0.5)
		ore = _mute(ore, 0.5)
		match family:
			"adit": _adit_spent(b, base, face, ore)
			"placer": _placer_spent(b, base, face, ore)
			"salt": _saltworks_spent(b, base, face)
			"head": _headframe_spent(b, base, face, ore)
		_tufts(b, 9)
		_dead_pin(b, _mute(Color(spec[2]), 0.45).darkened(0.35), h)
	else:
		match family:
			"adit": _adit(b, base, face, ore)
			"placer": _placer(b, base, face, ore)
			"salt": _saltworks(b, base, face)
			"head": _headframe(b, base, face, ore)
		DEP._pin(b, (Color(spec[2]).srgb_to_linear() * 0.4).linear_to_srgb(), h)
		_pennant(b, h)
	kit.meshes["deposit_mine%d%s%s%s" % [kind, "_head" if family == "head" else "", "_spent" if spent else "", "_tall" if tall else ""]] = b.commit()

## A colour greyed toward the weathered grey by `k`.
static func _mute(c: Color, k: float) -> Color:
	return c.lerp(WEATHERED, k)

## The pole of an abandoned working: leaning, weathered, its gem unlit (plain faces, not TAG_EMISSIVE) and dull.
static func _dead_pin(b: M7MeshBuilder, gem: Color, height: float) -> void:
	var foot := Vector3(0.11, 0.0, -0.06)
	var top := foot + Vector3(0.16, 1.0, 0.07).normalized() * height
	b.pole(foot, top, 0.009, OLD_TIMBER, 5)
	var r := DEP.GEM_R * 0.85
	var c := top + Vector3(0, r * 1.2, 0)
	var ring := [Vector3(r, 0, 0), Vector3(0, 0, r), Vector3(-r, 0, 0), Vector3(0, 0, -r)]
	var apex := c + Vector3(0, r * 1.35, 0)
	var nadir := c - Vector3(0, r * 1.35, 0)
	for i in 4:
		var p0: Vector3 = c + ring[i]
		var p1: Vector3 = c + ring[(i + 1) % 4]
		var shade := gem if i % 2 == 0 else gem.lightened(0.12)
		b.tri(apex, p0, p1, shade, c)
		b.tri(nadir, p1, p0, shade.darkened(0.15), c)

## Grass tufts coming back over the edge of the dug ground.
static func _tufts(b: M7MeshBuilder, n: int) -> void:
	for i in n:
		var a := TAU * (i + 0.5 * b.rnd()) / n
		var r := 0.15 + 0.05 * b.rnd()
		var p := Vector3(cos(a) * r, 0.004, sin(a) * r)
		if p.distance_to(Vector3(0.11, 0, -0.06)) < 0.04:
			continue
		b.blob(p, Vector3(0.016, 0.011 + 0.006 * b.rnd(), 0.016), 5, 2, REGROWTH.lerp(Color("#5f7a34"), b.rnd()), 0.25)

## A plank lying from a to b (a flat box turned along the line), raised `y` off the ground.
static func _plank(b: M7MeshBuilder, a: Vector3, e: Vector3, w: float, col: Color) -> void:
	var d := e - a
	var save := b.xf
	var yaw := atan2(d.x, d.z)
	var pitch := -atan2(d.y, Vector2(d.x, d.z).length())
	b.xf = save * Transform3D(Basis(Vector3.UP, yaw) * Basis(Vector3.RIGHT, pitch), (a + e) * 0.5)
	b.box(Vector3.ZERO, Vector3(w, 0.006, d.length()), col, col.lightened(0.08))
	b.xf = save

## A small swallow-tailed pennant in the working civilization's colour, just under the gem.
static func _pennant(b: M7MeshBuilder, h: float) -> void:
	var x := 0.11 + 0.008
	var z := -0.06
	var top := h - 0.008
	var bot := h - 0.062
	var save := b.tag
	b.tag = M7MeshBuilder.TAG_CIV
	b.quad(Vector3(x, top, z), Vector3(x + 0.07, top - 0.006, z + 0.012), Vector3(x + 0.05, (top + bot) * 0.5, z + 0.009),
		Vector3(x, (top + bot) * 0.5, z), Color.WHITE)
	b.quad(Vector3(x, (top + bot) * 0.5, z), Vector3(x + 0.05, (top + bot) * 0.5, z + 0.009), Vector3(x + 0.07, bot + 0.006, z + 0.012),
		Vector3(x, bot, z), Color.WHITE)
	b.tag = save

# ------------------------------------------------------------------ pieces

## A tipped heap of loose stone: a jittered cone in the spoil colour, its fresh top in the stain, with lumps of the stain down its flanks.
static func _heap(b: M7MeshBuilder, p: Vector3, r: Vector3, col: Color, stain: Color, lumps: int) -> void:
	var save := b.xf
	b.xf = save * Transform3D(Basis().scaled(Vector3(1.0, 1.0, r.z / r.x)), p)
	var rot := b.rnd() * TAU
	b.prism(Vector3.ZERO, r.x, r.x * 0.45, r.y * 0.62, 8, col, col.lightened(0.05), rot, 0.14, true)
	b.prism(Vector3(0, r.y * 0.62, 0), r.x * 0.45, r.x * 0.12, r.y * 0.38, 8, col.lerp(stain, 0.45), stain.lerp(col, 0.25), rot, 0.18)
	b.xf = save
	for i in lumps:
		var a := TAU * (i + 0.4 * b.rnd()) / lumps
		var f := 0.35 + 0.35 * b.rnd()
		var q := p + Vector3(cos(a) * r.x * f, r.y * (1.0 - f) * 0.9, sin(a) * r.z * f)
		b.blob(q, Vector3(0.014, 0.011, 0.013), 5, 2, stain, 0.2)

## A round open basket heaped with ore lumps.
static func _basket(b: M7MeshBuilder, p: Vector3, ore: Color) -> void:
	b.prism(p, 0.018, 0.024, 0.026, 7, BASKET, BASKET.darkened(0.3), b.rnd() * TAU, 0.0, true)
	b.prism(p + Vector3(0, 0.024, 0), 0.025, 0.025, 0.004, 7, BASKET.darkened(0.12), BASKET.darkened(0.12))
	for i in 4:
		var a := TAU * i / 4.0 + b.rnd()
		b.blob(p + Vector3(cos(a) * 0.009, 0.03, sin(a) * 0.009), Vector3(0.011, 0.009, 0.01), 5, 2, ore if i % 2 == 0 else ore.lightened(0.15), 0.2)

## A stone-headed maul or pick leaning on something.
static func _maul(b: M7MeshBuilder, foot: Vector3, head: Vector3) -> void:
	b.pole(foot, head, 0.0035, TIMBER, 4)
	var d := (head - foot).normalized()
	var side := d.cross(Vector3.UP).normalized() * 0.014
	b.pole(head - side, head + side, 0.008, Color("#6c675f"), 5)

## A flickering glow (fire, lamp); emissive is multiplied up in the shader, so it is pre-dimmed in linear light like the gem.
static func _glow(b: M7MeshBuilder, p: Vector3, r: Vector3, col: Color) -> void:
	var save := b.tag
	b.tag = M7MeshBuilder.TAG_EMISSIVE
	b.blob(p, r, 5, 2, (col.srgb_to_linear() * 0.45).linear_to_srgb())
	b.tag = save

# ------------------------------------------------------------------ adit (hard rock)

static func _adit(b: M7MeshBuilder, base: Color, face: Color, ore: Color) -> void:
	# the rock face: a broad dark mass split by veins, with the adit cut into its front (+Z)
	var rock := Vector3(-0.045, 0, -0.05)
	DEP._boulder(b, rock, Vector3(0.12, 0.1, 0.085), base, base.lightened(0.14))
	DEP._boulder(b, rock + Vector3(-0.09, 0, 0.03), Vector3(0.06, 0.06, 0.06), base.darkened(0.08), base.lightened(0.06))
	DEP._boulder(b, rock + Vector3(0.08, 0, -0.02), Vector3(0.05, 0.05, 0.05), base.darkened(0.05), base.lightened(0.08))
	for s in [[-0.1, -0.06, -0.3, 0.2, 0.03, 0.1], [0.01, -0.09, 0.25, -0.25, 0.028, 0.09], [-0.06, -0.1, -0.1, -0.4, 0.025, 0.08]]:
		DEP._shard(b, Vector3(s[0], 0.07, s[1]), Vector3(s[2], 0, s[3]), s[4], s[5], face, face.lightened(0.28))
	# the mouth: a dark recess flush with the rock front, framed by two props and a lintel
	var mx := -0.045
	var mz := 0.035
	var w := 0.034
	var top := 0.085
	b.box(Vector3(mx, top * 0.5, mz - 0.015), Vector3(w * 2.0, top, 0.03), DARK, DARK)
	_glow(b, Vector3(mx, 0.012, mz - 0.006), Vector3(0.018, 0.012, 0.01), EMBER)   # fire-setting against the face
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(mx + sx * (w + 0.004), 0, mz + 0.004), Vector3(mx + sx * (w + 0.002), top, mz + 0.004), 0.0065, TIMBER, 4)
	b.pole(Vector3(mx - w - 0.016, top + 0.004, mz + 0.004), Vector3(mx + w + 0.016, top + 0.004, mz + 0.004), 0.0085, TIMBER_DARK, 4)
	# the spoil heap thrown out to one side, stained with the ore
	_heap(b, Vector3(0.075, 0, 0.09), Vector3(0.07, 0.075, 0.06), SPOIL.lerp(base, 0.4), face, 4)
	# picked ore in baskets by the mouth, a heap of dressed lumps and the maul
	_basket(b, Vector3(-0.12, 0, 0.09), ore)
	_basket(b, Vector3(-0.07, 0, 0.13), ore)
	for i in 5:
		var a := TAU * i / 5.0
		b.blob(Vector3(-0.0 + cos(a) * 0.015, 0.008, 0.13 + sin(a) * 0.012), Vector3(0.014, 0.011, 0.013), 5, 2,
			ore.lightened(0.1 * (i % 2)), 0.2)
	_maul(b, Vector3(0.0, 0, 0.07), Vector3(-0.012, 0.06, 0.052))

# ------------------------------------------------------------------ placer (stream working)

static func _placer(b: M7MeshBuilder, base: Color, face: Color, ore: Color) -> void:
	var gravel := base.lerp(SPOIL, 0.5)
	# the washing channel across the working, cut into the ground and banked with gravel on both sides
	var a0 := Vector3(-0.2, 0, 0.0)
	var a1 := Vector3(0.18, 0, 0.05)
	var d := (a1 - a0).normalized()
	var nrm := Vector3(-d.z, 0, d.x)
	var hw := 0.026
	var up := Vector3(0, -1, 0)
	b.quad(a0 + nrm * (hw + 0.01) + Vector3(0, 0.005, 0), a1 + nrm * (hw + 0.01) + Vector3(0, 0.005, 0),
		a1 - nrm * (hw + 0.01) + Vector3(0, 0.005, 0), a0 - nrm * (hw + 0.01) + Vector3(0, 0.005, 0), EARTH.darkened(0.4), up)
	b.quad(a0 + nrm * hw + Vector3(0, 0.007, 0), a1 + nrm * hw + Vector3(0, 0.007, 0), a1 - nrm * hw + Vector3(0, 0.007, 0),
		a0 - nrm * hw + Vector3(0, 0.007, 0), WATER, up)
	b.quad(a0 + nrm * 0.007 + Vector3(0, 0.0085, 0), a1 + nrm * 0.007 + Vector3(0, 0.0085, 0), a1 - nrm * 0.003 + Vector3(0, 0.0085, 0),
		a0 - nrm * 0.003 + Vector3(0, 0.0085, 0), WATER_HI, up)
	for i in 7:
		var t := (i + 0.5) / 7.0
		var at := a0.lerp(a1, t)
		for sd in [-1.0, 1.0]:
			if sd < 0.0 and at.x > 0.04:
				continue   # keep the far bank clear of the pole
			var q: Vector3 = at + nrm * sd * (hw + 0.02 + 0.006 * b.rnd())
			var col := gravel.darkened(0.1 * b.rnd())
			if (i + int(sd > 0.0)) % 3 == 0:
				col = col.lerp(face, 0.5)
			b.blob(q + Vector3(0, 0.006, 0), Vector3(0.021, 0.013 + 0.006 * b.rnd(), 0.017), 5, 2, col, 0.2)
	# washed gravel heaps behind the channel, the last of the colour still in them
	_heap(b, Vector3(-0.11, 0, -0.105), Vector3(0.06, 0.06, 0.05), gravel, face, 3)
	_heap(b, Vector3(-0.005, 0, -0.125), Vector3(0.042, 0.042, 0.036), gravel.darkened(0.05), face, 2)
	# the sluice: a riffled trough on trestles, high end upstream, water running through it
	var s0 := Vector3(-0.15, 0.055, 0.105)
	var s1 := Vector3(0.05, 0.026, 0.14)
	var sdir := (s1 - s0).normalized()
	var sn := Vector3(-sdir.z, 0, sdir.x) * 0.022
	var wall := Vector3(0, 0.017, 0)
	var mid := (s0 + s1) * 0.5 + Vector3(0, 0.008, 0)
	b.quad(s0 + sn, s1 + sn, s1 - sn, s0 - sn, TIMBER, up)
	b.quad(s0 + sn * 0.8 + Vector3(0, 0.003, 0), s1 + sn * 0.8 + Vector3(0, 0.003, 0), s1 - sn * 0.8 + Vector3(0, 0.003, 0),
		s0 - sn * 0.8 + Vector3(0, 0.003, 0), WATER_HI, up)
	for sd in [-1.0, 1.0]:
		var e: Vector3 = sn * sd
		b.quad(s0 + e, s1 + e, s1 + e + wall, s0 + e + wall, TIMBER_DARK, mid)
	b.quad(s0 + sn, s0 - sn, s0 - sn + wall, s0 + sn + wall, TIMBER_DARK, mid)
	for i in 4:
		var q := s0.lerp(s1, (i + 1) / 5.0) + Vector3(0, 0.005, 0)
		b.pole(q + sn * 0.9, q - sn * 0.9, 0.003, TIMBER_DARK, 3)
	for t in [0.12, 0.8]:
		var q: Vector3 = s0.lerp(s1, t)
		for sd in [-1.0, 1.0]:
			b.pole(q + sn * sd * 1.15 + Vector3(0, -q.y, 0), q + sn * sd * 0.9, 0.0034, TIMBER, 3)
	# the dressed concentrate where the sluice spills, the heap the eye finds first
	var cpos := Vector3(0.1, 0, 0.15)
	b.prism(cpos, 0.036, 0.008, 0.038, 7, ore, ore.lightened(0.2), b.rnd() * TAU, 0.15, true)
	b.blob(cpos + Vector3(0.026, 0.006, 0.018), Vector3(0.014, 0.011, 0.013), 5, 2, ore.lightened(0.15), 0.15)
	# a wooden pan with a little colour in it, and a basket of gravel waiting to be washed
	b.prism(Vector3(0.015, 0.004, 0.195), 0.022, 0.03, 0.009, 9, TIMBER, TIMBER.lightened(0.12), 0.0, 0.0, true)
	b.blob(Vector3(0.015, 0.013, 0.195), Vector3(0.013, 0.004, 0.013), 6, 2, ore, 0.1)
	_basket(b, Vector3(-0.17, 0, 0.16), gravel)

# ------------------------------------------------------------------ salt works

static func _saltworks(b: M7MeshBuilder, base: Color, face: Color) -> void:
	var brine := Color("#86b6cc")
	var crust := face
	# a 3 x 2 grid of evaporation pans, brine at one end drying to white crust at the other
	var pw := 0.07
	var pd := 0.072
	var x0 := -0.19
	var z0 := -0.125
	var stages := [brine, brine.lerp(crust, 0.5), crust]
	for ix in 3:
		for iz in 2:
			var cx := x0 + pw * 0.5 + ix * (pw + 0.009)
			var cz := z0 + pd * 0.5 + iz * (pd + 0.009)
			var col: Color = stages[ix]
			b.box(Vector3(cx, 0.0075, cz), Vector3(pw, 0.007, pd), CLAY.darkened(0.15), col)
			for sd in [-1.0, 1.0]:
				b.box(Vector3(cx + sd * pw * 0.5, 0.011, cz), Vector3(0.009, 0.014, pd + 0.009), CLAY, CLAY.lightened(0.12))
				b.box(Vector3(cx, 0.011, cz + sd * pd * 0.5), Vector3(pw + 0.009, 0.014, 0.009), CLAY, CLAY.lightened(0.12))
	# raked white heaps along the dry end
	for p in [[0.085, 0.08, 0.042, 0.068], [0.02, 0.125, 0.034, 0.052], [0.155, 0.03, 0.03, 0.046], [0.14, 0.13, 0.026, 0.04]]:
		b.prism(Vector3(p[0], 0, p[1]), p[2], p[2] * 0.12, p[3], 8, crust, crust, b.rnd() * TAU, 0.1, true)
		b.prism(Vector3(p[0], 0, p[1]), p[2] * 1.12, p[2] * 0.95, 0.007, 8, crust.darkened(0.14), crust.darkened(0.06))
	# a rake leaning on the biggest heap
	b.pole(Vector3(0.03, 0.0, 0.06), Vector3(0.085, 0.07, 0.085), 0.003, TIMBER, 3)
	b.pole(Vector3(0.016, 0.004, 0.07), Vector3(0.044, 0.004, 0.05), 0.0035, TIMBER_DARK, 3)
	# a thatched drying shelter over baskets of salt
	var c := Vector3(-0.115, 0, 0.125)
	var hh := 0.04
	var eave := 0.068
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			b.pole(c + Vector3(sx * hh, 0, sz * hh), c + Vector3(sx * hh, eave + 0.004, sz * hh), 0.0045, TIMBER, 4)
	b.prism(c + Vector3(0, eave, 0), 0.085, 0.006, 0.055, 4, THATCH, THATCH_DARK, PI * 0.25, 0.0, true)
	_basket(b, c + Vector3(-0.016, 0, -0.008), crust)
	_basket(b, c + Vector3(0.02, 0, 0.012), crust)

# ------------------------------------------------------------------ headframe (mine head over a shaft)

static func _headframe(b: M7MeshBuilder, base: Color, face: Color, ore: Color) -> void:
	var c := Vector3(-0.05, 0, -0.01)    # shaft centre
	# the shaft: a dark square hole in a collar of four squared logs
	b.box(c + Vector3(0, 0.0055, 0), Vector3(0.062, 0.003, 0.062), DARK, DARK)
	for i in 4:
		var a := PI * 0.5 * i
		var along := Vector3(cos(a), 0, sin(a))
		var off := Vector3(-sin(a), 0, cos(a)) * 0.039
		b.pole(c + off - along * 0.052 + Vector3(0, 0.009, 0), c + off + along * 0.052 + Vector3(0, 0.009, 0), 0.008,
			TIMBER if i % 2 == 0 else TIMBER_DARK, 4)
	# a tapering four-legged tower over the shaft, braced twice, with a cap that carries the sheave wheel
	var top := 0.25
	var foot := [Vector3(-0.072, 0, -0.064), Vector3(0.072, 0, -0.064), Vector3(0.072, 0, 0.064), Vector3(-0.072, 0, 0.064)]
	var head := [Vector3(-0.024, top, -0.018), Vector3(0.024, top, -0.018), Vector3(0.024, top, 0.018), Vector3(-0.024, top, 0.018)]
	for i in 4:
		b.pole(c + foot[i], c + head[i], 0.0065, TIMBER, 4)
	for f in [0.36, 0.7]:
		for i in 4:
			var p0: Vector3 = c + (foot[i] as Vector3).lerp(head[i], f)
			var p1: Vector3 = c + (foot[(i + 1) % 4] as Vector3).lerp(head[(i + 1) % 4], f)
			b.pole(p0, p1, 0.0042, TIMBER_DARK, 4)
	for i in [1, 3]:
		b.pole(c + foot[i], c + (foot[(i + 1) % 4] as Vector3).lerp(head[(i + 1) % 4], 0.36), 0.0035, TIMBER_DARK, 3)
	b.box(c + Vector3(0, top + 0.004, 0), Vector3(0.066, 0.008, 0.05), TIMBER_DARK, TIMBER)
	# the sheave: a spoked wheel standing on the cap, facing the yard
	var wc := c + Vector3(0, top + 0.036, 0)
	var wr := 0.03
	for sd in [-1.0, 1.0]:
		b.pole(c + Vector3(sd * 0.012, top + 0.008, 0.0), wc + Vector3(sd * 0.004, 0, 0.0), 0.0035, TIMBER_DARK, 3)
	for i in 8:
		var a0 := TAU * i / 8.0
		var a1 := TAU * (i + 1) / 8.0
		b.pole(wc + Vector3(cos(a0), sin(a0), 0) * wr, wc + Vector3(cos(a1), sin(a1), 0) * wr, 0.0038, TIMBER.lightened(0.1), 4)
	for i in 2:
		var a := PI * 0.5 * i + PI * 0.25
		b.pole(wc - Vector3(cos(a), sin(a), 0) * wr, wc + Vector3(cos(a), sin(a), 0) * wr, 0.0022, TIMBER_DARK, 3)
	# the rope: from the wheel down the shaft to a bucket coming up full, and off the far side to the windlass
	var bucket := c + Vector3(wr, 0.07, 0)
	b.pole(wc + Vector3(wr, 0, 0), bucket + Vector3(0, 0.028, 0), 0.0018, ROPE, 3)
	b.prism(bucket, 0.012, 0.016, 0.026, 7, BASKET.darkened(0.1), BASKET.darkened(0.35), 0.0, 0.0, true)
	b.blob(bucket + Vector3(0, 0.026, 0), Vector3(0.013, 0.008, 0.013), 5, 2, ore, 0.2)
	var wl := Vector3(-0.17, 0.045, 0.0)
	for sd in [-1.0, 1.0]:
		b.pole(Vector3(wl.x, 0, sd * 0.05), Vector3(wl.x, wl.y + 0.004, sd * 0.05), 0.0055, TIMBER, 4)
	b.pole(wl + Vector3(0, 0, -0.058), wl + Vector3(0, 0, 0.058), 0.011, TIMBER.lightened(0.08), 6)
	b.pole(wl + Vector3(0, 0, -0.014), wl + Vector3(0, 0, 0.014), 0.0135, ROPE.darkened(0.1), 6)
	b.pole(wl + Vector3(0, 0, 0.058), wl + Vector3(0.012, -0.018, 0.068), 0.0035, TIMBER_DARK, 3)
	b.pole(wl + Vector3(0.006, 0.01, 0), wc + Vector3(-wr, 0, 0), 0.0018, ROPE, 3)
	# a lamp hung inside the near legs
	var leg: Vector3 = c + (foot[3] as Vector3).lerp(head[3], 0.36) + Vector3(0.02, 0, 0)
	b.pole(leg, leg + Vector3(0, -0.016, 0), 0.0015, ROPE, 3)
	_glow(b, leg + Vector3(0, -0.024, 0), Vector3(0.008, 0.01, 0.008), LAMP)
	# the spoil of the shaft, big and stained, and the picked ore waiting
	_heap(b, Vector3(0.085, 0, 0.1), Vector3(0.08, 0.085, 0.065), SPOIL.lerp(base, 0.4), face, 5)
	_basket(b, Vector3(-0.1, 0, 0.13), ore)
	_basket(b, Vector3(-0.045, 0, 0.15), ore)
	_basket(b, Vector3(0.03, 0, -0.13), ore)

# ------------------------------------------------------------------ spent workings

## The adit caved in: rubble fills the mouth, the lintel has dropped at one end, one prop lies on the ground, two boards are nailed
## across what is left; the spoil heap has slumped and greyed, an empty basket lies on its side.
static func _adit_spent(b: M7MeshBuilder, base: Color, face: Color, ore: Color) -> void:
	var rock := Vector3(-0.045, 0, -0.05)
	DEP._boulder(b, rock, Vector3(0.12, 0.1, 0.085), base, base.lightened(0.1))
	DEP._boulder(b, rock + Vector3(-0.09, 0, 0.03), Vector3(0.06, 0.06, 0.06), base.darkened(0.08), base.lightened(0.05))
	DEP._boulder(b, rock + Vector3(0.08, 0, -0.02), Vector3(0.05, 0.05, 0.05), base.darkened(0.05), base.lightened(0.06))
	for s in [[-0.1, -0.06, -0.3, 0.2, 0.026, 0.08], [0.01, -0.09, 0.25, -0.25, 0.024, 0.07]]:
		DEP._shard(b, Vector3(s[0], 0.07, s[1]), Vector3(s[2], 0, s[3]), s[4], s[5], face, face.lightened(0.15))
	var mx := -0.045
	var mz := 0.035
	var w := 0.034
	var top := 0.085
	b.box(Vector3(mx, top * 0.5, mz - 0.015), Vector3(w * 2.0, top, 0.03), DARK, DARK)
	# the fall: a rubble slope filling the mouth
	b.blob(Vector3(mx, 0.0, mz + 0.004), Vector3(0.045, 0.05, 0.03), 6, 3, SPOIL.lerp(base, 0.5), 0.2, base)
	for i in 4:
		b.blob(Vector3(mx - 0.03 + 0.02 * i, 0.006, mz + 0.03 + 0.008 * b.rnds()), Vector3(0.013, 0.01, 0.012), 5, 2, SPOIL.lerp(base, 0.3), 0.2)
	# the left prop still stands, the right one has fallen out; the lintel hangs from the left
	b.pole(Vector3(mx - w - 0.004, 0, mz + 0.004), Vector3(mx - w - 0.002, top, mz + 0.004), 0.0065, OLD_TIMBER, 4)
	b.pole(Vector3(mx - w - 0.012, top + 0.004, mz + 0.004), Vector3(mx + w + 0.004, 0.028, mz + 0.012), 0.0085, OLD_TIMBER.darkened(0.15), 4)
	b.pole(Vector3(mx + w + 0.02, 0.006, mz + 0.03), Vector3(mx + w + 0.02 + 0.07, 0.006, mz + 0.06), 0.0065, OLD_TIMBER, 4)
	# boards across the mouth
	_plank(b, Vector3(mx - w - 0.006, 0.07, mz + 0.012), Vector3(mx + w + 0.004, 0.045, mz + 0.012), 0.012, OLD_TIMBER.lightened(0.1))
	_plank(b, Vector3(mx - w - 0.006, 0.035, mz + 0.016), Vector3(mx + w + 0.004, 0.06, mz + 0.016), 0.012, OLD_TIMBER)
	_heap(b, Vector3(0.075, 0, 0.09), Vector3(0.075, 0.05, 0.065), WEATHERED.lerp(base, 0.35), face, 2)
	# an empty basket on its side
	var save := b.xf
	b.xf = save * Transform3D(Basis(Vector3.FORWARD, PI * 0.5), Vector3(-0.12, 0.022, 0.1))
	b.prism(Vector3.ZERO, 0.018, 0.024, 0.026, 7, BASKET.lerp(WEATHERED, 0.5), DARK, 0.3, 0.0, true)
	b.xf = save

## The headframe fallen: its shaft collar boarded over, two legs left standing as broken stumps, the rest of the tower lying across
## the yard with the sheave wheel flat in the grass, the windlass drum rolled off its posts; the spoil heap grey and grown over.
static func _headframe_spent(b: M7MeshBuilder, base: Color, face: Color, ore: Color) -> void:
	var c := Vector3(-0.05, 0, -0.01)
	b.box(c + Vector3(0, 0.0055, 0), Vector3(0.062, 0.003, 0.062), DARK, DARK)
	for i in 4:
		var a := PI * 0.5 * i
		var along := Vector3(cos(a), 0, sin(a))
		var off := Vector3(-sin(a), 0, cos(a)) * 0.039
		b.pole(c + off - along * 0.052 + Vector3(0, 0.009, 0), c + off + along * 0.052 + Vector3(0, 0.009, 0), 0.008, OLD_TIMBER, 4)
	for i in 4:   # boards across the shaft
		var z := -0.027 + 0.018 * i
		_plank(b, c + Vector3(-0.045, 0.016, z), c + Vector3(0.045, 0.016, z + 0.004 * b.rnds()), 0.014,
			OLD_TIMBER.lightened(0.06 * (i % 2)))
	# broken stumps of the two back legs
	for x in [-0.072, 0.072]:
		b.pole(c + Vector3(x, 0, -0.064), c + Vector3(x * 0.86, 0.085 + 0.02 * b.rnd(), -0.05), 0.0065, OLD_TIMBER, 4)
	# the fallen tower: two long legs and a brace lying forward across the yard, the cap and wheel at their far end
	var fall := Vector3(0.07, 0, 0.14)
	b.pole(c + Vector3(-0.072, 0.006, 0.064), c + fall + Vector3(-0.04, 0.006, 0.05), 0.0065, OLD_TIMBER, 4)
	b.pole(c + Vector3(0.072, 0.006, 0.064), c + fall + Vector3(0.03, 0.006, 0.02), 0.0065, OLD_TIMBER, 4)
	b.pole(c + Vector3(-0.03, 0.006, 0.12), c + Vector3(0.11, 0.006, 0.09), 0.0042, OLD_TIMBER.darkened(0.15), 4)
	b.pole(c + Vector3(-0.08, 0.005, -0.02), c + Vector3(-0.13, 0.005, 0.1), 0.0042, OLD_TIMBER.darkened(0.1), 4)
	var wc := c + fall + Vector3(0.0, 0.006, 0.06)
	var wr := 0.03
	for i in 8:
		var a0 := TAU * i / 8.0
		var a1 := TAU * (i + 1) / 8.0
		b.pole(wc + Vector3(cos(a0), 0, sin(a0)) * wr, wc + Vector3(cos(a1), 0, sin(a1)) * wr, 0.0038, OLD_TIMBER.lightened(0.08), 4)
	b.pole(wc - Vector3(wr, 0, 0), wc + Vector3(wr, 0, 0), 0.0022, OLD_TIMBER.darkened(0.15), 3)
	# the windlass drum rolled into the grass beside its leaning posts
	b.pole(Vector3(-0.17, 0, -0.05), Vector3(-0.17, 0.045, -0.05), 0.0055, OLD_TIMBER, 4)
	b.pole(Vector3(-0.17, 0, 0.05), Vector3(-0.19, 0.035, 0.06), 0.0055, OLD_TIMBER, 4)
	b.pole(Vector3(-0.15, 0.011, 0.07), Vector3(-0.09, 0.011, 0.14), 0.011, OLD_TIMBER.lightened(0.05), 6)
	_heap(b, Vector3(0.1, 0, -0.0), Vector3(0.07, 0.065, 0.06), WEATHERED.lerp(base, 0.35), face, 2)
	_heap(b, Vector3(0.0, 0, -0.14), Vector3(0.05, 0.04, 0.04), WEATHERED.lerp(base, 0.3), face, 1)

## The stream working given up: the channel run dry and sanded up, the gravel banks and heaps slumped low, the sluice down off its
## trestles in two pieces, the pan upside down.
static func _placer_spent(b: M7MeshBuilder, base: Color, face: Color, ore: Color) -> void:
	var gravel := base.lerp(WEATHERED, 0.5)
	var a0 := Vector3(-0.2, 0, 0.0)
	var a1 := Vector3(0.18, 0, 0.05)
	var d := (a1 - a0).normalized()
	var nrm := Vector3(-d.z, 0, d.x)
	var hw := 0.026
	var up := Vector3(0, -1, 0)
	var dry := Color("#958870")
	b.quad(a0 + nrm * (hw + 0.01) + Vector3(0, 0.005, 0), a1 + nrm * (hw + 0.01) + Vector3(0, 0.005, 0),
		a1 - nrm * (hw + 0.01) + Vector3(0, 0.005, 0), a0 - nrm * (hw + 0.01) + Vector3(0, 0.005, 0), EARTH.darkened(0.2), up)
	b.quad(a0 + nrm * hw + Vector3(0, 0.0065, 0), a1 + nrm * hw + Vector3(0, 0.0065, 0), a1 - nrm * hw + Vector3(0, 0.0065, 0),
		a0 - nrm * hw + Vector3(0, 0.0065, 0), dry, up)
	for i in 5:   # a little water left standing in puddles, and stones in the bed
		var q := a0.lerp(a1, (i + 0.5) / 5.0) + nrm * 0.006 * b.rnds()
		if i % 2 == 0:
			b.prism(q + Vector3(0, 0.0068, 0), 0.012, 0.012, 0.0006, 6, WATER.lerp(dry, 0.45), WATER.lerp(dry, 0.45), b.rnd() * TAU)
		else:
			b.blob(q + Vector3(0, 0.008, 0), Vector3(0.008, 0.005, 0.007), 5, 2, gravel.darkened(0.1), 0.2)
	for i in 6:
		var at := a0.lerp(a1, (i + 0.5) / 6.0)
		for sd in [-1.0, 1.0]:
			if sd < 0.0 and at.x > 0.04:
				continue
			var q: Vector3 = at + nrm * sd * (hw + 0.02)
			b.blob(q + Vector3(0, 0.004, 0), Vector3(0.024, 0.008, 0.02), 5, 2, gravel.darkened(0.08 * b.rnd()), 0.2)
	_heap(b, Vector3(-0.11, 0, -0.105), Vector3(0.07, 0.035, 0.058), gravel, face, 1)
	_heap(b, Vector3(-0.005, 0, -0.125), Vector3(0.048, 0.024, 0.04), gravel.darkened(0.05), face, 0)
	# the sluice: the trough lies in the grass, a trestle still standing, a loose board
	_plank(b, Vector3(-0.15, 0.03, 0.12), Vector3(-0.02, 0.006, 0.16), 0.04, OLD_TIMBER)
	_plank(b, Vector3(-0.15, 0.04, 0.142), Vector3(-0.02, 0.016, 0.182), 0.006, OLD_TIMBER.darkened(0.12))
	for sd in [-1.0, 1.0]:
		b.pole(Vector3(-0.13, 0, 0.1 + sd * 0.024), Vector3(-0.135, 0.045, 0.105 + sd * 0.02), 0.0034, OLD_TIMBER, 3)
	_plank(b, Vector3(0.02, 0.006, 0.1), Vector3(0.1, 0.006, 0.13), 0.012, OLD_TIMBER.lightened(0.08))
	# the pan upside down by the old spill
	b.prism(Vector3(0.1, 0.004, 0.16), 0.03, 0.022, 0.01, 9, OLD_TIMBER, OLD_TIMBER.lightened(0.1), 0.0, 0.0, true)

## The salt works abandoned: the pans dry, grey and cracked, rims broken through, two last grey heaps, the shelter's roof down on the
## ground beside its posts.
static func _saltworks_spent(b: M7MeshBuilder, base: Color, face: Color) -> void:
	var dried := Color("#c9c1b0").lerp(face, 0.3)
	var pw := 0.07
	var pd := 0.072
	var x0 := -0.19
	var z0 := -0.125
	var rim := CLAY.lerp(WEATHERED, 0.45)
	for ix in 3:
		for iz in 2:
			var cx := x0 + pw * 0.5 + ix * (pw + 0.009)
			var cz := z0 + pd * 0.5 + iz * (pd + 0.009)
			var col := dried.darkened(0.05 * ((ix + iz) % 2))
			b.box(Vector3(cx, 0.0075, cz), Vector3(pw, 0.007, pd), rim.darkened(0.15), col)
			# crazed with cracks
			for k in 3:
				var p0 := Vector3(cx + pw * 0.4 * b.rnds(), 0.0115, cz + pd * 0.4 * b.rnds())
				var p1 := p0 + Vector3(0.022 * b.rnds(), 0, 0.022 * b.rnds())
				b.pole(p0, p1, 0.0012, dried.darkened(0.35), 3)
			for sd in [-1.0, 1.0]:
				if (ix * 2 + iz + int(sd > 0.0)) % 5 != 1:   # some rims have washed out
					b.box(Vector3(cx + sd * pw * 0.5, 0.0095, cz), Vector3(0.009, 0.01, pd + 0.009), rim, rim.lightened(0.1))
				if (ix + iz * 3 + int(sd > 0.0)) % 4 != 2:
					b.box(Vector3(cx, 0.0095, cz + sd * pd * 0.5), Vector3(pw + 0.009, 0.01, 0.009), rim, rim.lightened(0.1))
	var heap := face.lerp(WEATHERED, 0.35)
	for p in [[0.085, 0.08, 0.036, 0.04], [0.15, 0.03, 0.024, 0.026]]:
		b.prism(Vector3(p[0], 0, p[1]), p[2], p[2] * 0.35, p[3], 8, heap, heap.lightened(0.05), b.rnd() * TAU, 0.14, true)
	# the shelter: two posts still up, the roof slid off onto the ground
	var c := Vector3(-0.115, 0, 0.125)
	b.pole(c + Vector3(-0.04, 0, -0.04), c + Vector3(-0.04, 0.072, -0.04), 0.0045, OLD_TIMBER, 4)
	b.pole(c + Vector3(-0.04, 0, 0.04), c + Vector3(-0.035, 0.06, 0.045), 0.0045, OLD_TIMBER, 4)
	var save := b.xf
	b.xf = save * Transform3D(Basis(Vector3.FORWARD, -0.5), c + Vector3(0.035, 0.012, 0.01))
	b.prism(Vector3.ZERO, 0.07, 0.006, 0.045, 4, THATCH.lerp(WEATHERED, 0.45), THATCH_DARK.lerp(WEATHERED, 0.45), PI * 0.25, 0.08, true)
	b.xf = save
