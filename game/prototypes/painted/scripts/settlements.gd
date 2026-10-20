## Lays out every settlement deterministically from its structure counts (the sim gives no positions),
## and places the frame's figures and herds. Structure order (MatchFrame): Hearth, LeanTo, HideTent, PitHouse,
## WattleHouse, MudbrickHouse, Longhouse, StoragePit, DryingRack, Granary, Kiln, Quern, ThornBarrier, Lookout,
## Weir, Field, Pasture, Orchard.
extends RefCounted

const FrameData = preload("res://scripts/frame_data.gd")
const TerrainBake = preload("res://scripts/terrain_bake.gd")
const Palette = preload("res://scripts/palette.gd")
const Instancer = preload("res://scripts/instancer.gd")

const DWELLINGS := [[1, "lean_to"], [2, "tent"], [3, "pit_house"], [4, "wattle"], [5, "mudbrick"], [6, "longhouse"]]
const S_OPTS := {"far": 160.0, "size": 0.06, "flat": 1.0, "hull_y": 0.025, "outline": 1.4}
const P_OPTS := {"far": 75.0, "size": 0.04, "outline": 1.2, "shadow": true}
const A_OPTS := {"far": 75.0, "size": 0.035, "outline": 1.1}
const SPECIES_COL := ["#a0703f", "#4a382c", "#5a4a3e", "#5e4230", "#8a5a36", "#8c7f6e", "#e3dccb", "#c69a5e", "#c7a776", "#9a8064"]
const SPECIES_SCALE := [1.0, 1.3, 0.8, 1.35, 1.2, 1.05, 0.78, 0.85, 1.3, 0.5]
const KS := 1.7 # structures are drawn larger than life so the village leads the eye
const KP := 1.5 # people and animals
const SPECIES_HORNED := [false, true, false, true, false, true, true, true, false, false]


static func h2(x: float, y: float) -> float:
	var v := sin(x * 12.9898 + y * 78.233) * 43758.5453
	return v - floorf(v)


static func _civ(civ: int) -> Color:
	var c: Color = Palette.CIV[civ % 8].srgb_to_linear()
	c.a = 0.0
	return c


static func _put(inst: Instancer, mesh: String, bake: TerrainBake, p: Vector2, yaw: float, s: float, custom: Color, opts := S_OPTS, sink := 0.004) -> void:
	s *= KP if opts == P_OPTS or opts == A_OPTS else KS
	var basis := Basis(Vector3.UP, yaw).scaled(Vector3(s, s, s))
	inst.add(mesh, Transform3D(basis, Vector3(p.x, bake.height_at(p) - sink, p.y)), custom, opts)


static func pasture_center(c: Vector2, si: int, pi: int) -> Vector2:
	var a := si * 1.3 + pi * 2.4 + 2.2
	return c + Vector2(cos(a), sin(a)) * (1.12 + pi * 0.06)


## Areas kept clear of trees: pastures (x, z, radius).
static func reserved(fd: FrameData) -> Array:
	var out := []
	for si in fd.settlements.size():
		var s: Dictionary = fd.settlements[si]
		for pi in int(s.structures[16]):
			var pc := pasture_center(s.pos, si, pi)
			out.append(Vector3(pc.x, pc.y, 0.34))
	return out


## Returns [{pos: Vector3, civ, size, settlement}] fire spots and flag spots for main.gd.
static func build(fd: FrameData, bake: TerrainBake, inst: Instancer) -> Dictionary:
	var fires := []
	var flags := []
	var neutral := Color(1, 1, 1, 0)
	for si in fd.settlements.size():
		var s: Dictionary = fd.settlements[si]
		var c: Vector2 = s.pos
		var civc := _civ(s.civ)
		var st: Array = s.structures
		var pop: int = s.population
		_put(inst, "hearth", bake, c, 0.0, 1.0, neutral)
		fires.append({"pos": Vector3(c.x, bake.height_at(c), c.y), "size": 1.3, "civ": s.civ})
		# dwellings on rings around the hearth, doors facing the fire
		var avail := []
		for d in DWELLINGS:
			if int(st[d[0]]) > 0:
				avail.append(d[1])
		if avail.is_empty():
			avail.append("tent")
		var best: String = avail[avail.size() - 1]
		var n := clampi(int(ceil(pop / 4.0)), 2, 14)
		var rings := [[0.25, 5], [0.39, 8], [0.53, 10], [0.66, 12]]
		var placed := 0
		var ring_i := 0
		var slot := 0
		var guard := 0
		while placed < n and guard < 80:
			guard += 1
			var ring: Array = rings[ring_i]
			var cap: int = ring[1]
			var a := TAU * slot / cap + ring_i * 0.9 + si * 1.7 + (h2(si, guard) - 0.5) * 0.35
			var r: float = ring[0] + (h2(guard, si) - 0.5) * 0.05
			var dir := Vector2(cos(a), sin(a))
			var p := c + dir * r
			slot += 1
			if slot >= cap:
				slot = 0
				ring_i = mini(ring_i + 1, rings.size() - 1)
			if bake.raw_height(p) < 0.02 or bake.river_dist(p) < 0.07:
				continue
			# skip a wedge to the south-east: the path out to the fields and the banner
			if dir.dot(Vector2(0.7, 0.7)) > 0.93:
				continue
			var kind: String = best if h2(si * 3.0, placed) < 0.55 else avail[int(h2(placed, si * 7.0) * avail.size()) % avail.size()]
			var yaw := atan2(dir.y, -dir.x)
			if kind == "longhouse":
				yaw += PI * 0.5
			var sc := 0.92 + 0.18 * h2(placed, si)
			_put(inst, kind, bake, p, yaw, sc, civc)
			placed += 1
		# stores and workplaces close to the hearth
		var inner := [[7, "storage_pit", 0.13], [11, "quern", 0.1], [8, "drying_rack", 0.17], [9, "granary", 0.33], [10, "kiln", 0.43]]
		var k := 0
		for e in inner:
			var cnt: int = int(st[e[0]])
			if e[0] == 7:
				cnt = maxi(cnt, 1) + int(pop / 15)
			for i in cnt:
				k += 1
				var a := k * 2.39996 + si
				var p: Vector2 = c + Vector2(cos(a), sin(a)) * (e[2] + 0.02 * h2(k, si))
				if bake.raw_height(p) < 0.02:
					continue
				_put(inst, e[1], bake, p, a, 1.0, civc)
		# lookout on the rim
		if int(st[13]) > 0:
			var a := si * 2.1 + 0.6
			var p := c + Vector2(cos(a), sin(a)) * 0.72
			if bake.raw_height(p) > 0.02:
				_put(inst, "lookout", bake, p, a, 1.0, civc)
		# thorn barrier ring with a gate gap
		if int(st[12]) > 0:
			var R := 0.82
			var segs := int(TAU * R / 0.17)
			for i in segs:
				var a := TAU * i / segs
				var d := Vector2(cos(a), sin(a))
				if d.dot(Vector2(0.7, 0.7)) > 0.96:
					continue
				var p := c + d * (R + 0.02 * sin(i * 1.7))
				if bake.raw_height(p) < 0.02:
					continue
				_put(inst, "thorn", bake, p, -a + PI * 0.5, 0.8, neutral, {"far": 100.0, "size": 0.03, "outline": 0.7, "shadow": false})
		# pastures: fenced pens with a few goats
		for pi in int(st[16]):
			var pc := pasture_center(c, si, pi)
			if bake.raw_height(pc) < 0.03:
				continue
			var pr := 0.2
			var posts := 16
			for i in posts:
				var b := TAU * i / posts
				var p := pc + Vector2(cos(b), sin(b)) * pr
				var tang := b + PI * 0.5
				_put(inst, "fence", bake, p, -tang, pr * TAU / posts / 0.06 / KS, neutral, {"far": 60.0, "size": 0.02, "outline": 0.9}, 0.002)
			for g in 4:
				var gp := pc + Vector2(h2(g, pi + si) - 0.5, h2(si, g + pi * 3.0) - 0.5) * pr * 1.1
				var cc := Color(Color(SPECIES_COL[6]).srgb_to_linear(), 0.0)
				_put(inst, "animal_horned", bake, gp, h2(g, si) * TAU, 0.75, cc, A_OPTS, 0.0)
		# weir: a line of stakes across the nearest river
		if int(st[14]) > 0:
			var best_p := Vector2.ZERO
			var best_d := 99.0
			for i in 48:
				var a := TAU * i / 48.0
				for rr in [0.4, 0.6, 0.8, 1.0, 1.2]:
					var p: Vector2 = c + Vector2(cos(a), sin(a)) * rr
					var d := bake.river_dist(p)
					if d < best_d:
						best_d = d
						best_p = p
			if best_d < 0.15:
				var e := 0.05
				var g := Vector2(bake.river_dist(best_p + Vector2(e, 0)) - bake.river_dist(best_p - Vector2(e, 0)),
					bake.river_dist(best_p + Vector2(0, e)) - bake.river_dist(best_p - Vector2(0, e))).normalized()
				for i in range(-3, 4):
					var p := best_p + g * (i * 0.022)
					_put(inst, "stake", bake, p, i, 1.0, neutral, {"far": 50.0, "size": 0.02, "outline": 0.9}, 0.0)
		flags.append({"pos": Vector3(c.x + 0.1, bake.height_at(c + Vector2(0.1, 0.08)), c.y + 0.08), "civ": s.civ, "scale": KS})
		_put(inst, "pole", bake, c + Vector2(0.1, 0.08), 0.0, 1.0, civc, S_OPTS, 0.0)
	# seasonal camps: a tent or two around a small fire, and a short pennant
	for ci in fd.camps.size():
		var cp: Dictionary = fd.camps[ci]
		var c: Vector2 = cp.pos
		if bake.raw_height(c) < 0.02:
			continue
		var civc := _civ(cp.civ)
		_put(inst, "hearth", bake, c, 0.0, 0.8, neutral)
		fires.append({"pos": Vector3(c.x, bake.height_at(c), c.y), "size": 0.7, "civ": cp.civ})
		for i in 2:
			var a := ci * 1.9 + i * 2.6
			var p := c + Vector2(cos(a), sin(a)) * 0.16
			_put(inst, "tent", bake, p, atan2(sin(a), -cos(a)), 0.9, civc)
		flags.append({"pos": Vector3(c.x - 0.07, bake.height_at(c + Vector2(-0.07, 0.07)), c.y + 0.07), "civ": cp.civ, "scale": 0.6 * KS})
		_put(inst, "pole", bake, c + Vector2(-0.07, 0.07), 0.0, 0.6, civc, S_OPTS, 0.0)
	return {"fires": fires, "flags": flags}


static func people(fd: FrameData, bake: TerrainBake, inst: Instancer) -> int:
	var n := 0
	for i in fd.figures.size():
		var f: Dictionary = fd.figures[i]
		var p: Vector2 = f.pos
		if bake.raw_height(p) < 0.0:
			continue
		var mesh := "adult"
		if f.age == 0 or f.activity == 13:
			mesh = "child"
		elif f.age == 2:
			mesh = "elder"
		elif f.activity == 2 or (f.flags & (128 | 256)) != 0:
			mesh = "adult_spear"
		var col := _civ(f.civ)
		if (f.flags & 1024) != 0:
			col = Color(0.35, 0.35, 0.35, 0.0)
		var yaw := h2(i, 3.0) * TAU
		_put(inst, mesh, bake, p, yaw, 1.0, col, P_OPTS, 0.0)
		n += 1
	return n


static func herds(fd: FrameData, bake: TerrainBake, inst: Instancer) -> int:
	var n := 0
	for i in fd.herds.size():
		var hd: Dictionary = fd.herds[i]
		var sp: int = hd.species
		var count := clampi(int(sqrt(float(hd.animals)) * 0.8), 2, 9)
		var heading := h2(i, 1.0) * TAU
		var col := Color(Color(SPECIES_COL[sp]).srgb_to_linear(), 0.0)
		var mesh := "animal_horned" if SPECIES_HORNED[sp] else "animal"
		var R := 0.06 + 0.022 * count
		for k in count:
			var p: Vector2 = hd.pos + Vector2(h2(i, k * 2.0) - 0.5, h2(k * 3.0, i) - 0.5) * R * 2.0
			if bake.raw_height(p) < 0.02:
				continue
			var cc := Color(col.r * (0.9 + 0.2 * h2(k, i)), col.g * (0.9 + 0.2 * h2(k, i)), col.b * (0.9 + 0.2 * h2(k, i)), 0.0)
			_put(inst, mesh, bake, p, heading + (h2(i, k) - 0.5) * 0.9, SPECIES_SCALE[sp] * (0.9 + 0.2 * h2(k + 1.0, i)), cc, A_OPTS, 0.0)
			n += 1
	return n
