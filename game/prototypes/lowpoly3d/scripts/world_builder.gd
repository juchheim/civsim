## Turns a WorldData frame into a diorama: terrain, water, rivers, borders, vegetation,
## settlements (procedural layout from structure counts), camps, people and herds.
class_name WorldBuilder
extends RefCounted

const SPECIES := [
	# mesh, colour, scale
	["animal_slender", "#8b5a33", 1.0],   # Deer
	["animal_heavy", "#3b2c22", 1.05],    # Aurochs
	["animal_heavy", "#4a3d33", 0.72],    # Wild boar
	["animal_heavy", "#5a3d26", 1.12],    # Bison
	["animal_slender", "#9a6a3c", 1.18],  # Horse
	["animal_slender", "#8c8478", 1.0],   # Reindeer
	["animal_woolly", "#e3d9c3", 1.0],    # Wild goat / sheep
	["animal_slender", "#c79a5f", 0.85],  # Gazelle / antelope
	["animal_slender", "#c8a26e", 1.35],  # Camel / llama
	["animal_small", "#8f8372", 1.0],     # Small game
]

var w: WorldData
var kit: MeshKit
var mats: Dictionary
var props := Batcher.new()
var root: Node3D
var hearths := []          # world positions of settlement hearths (index = settlement)
var villages := []         # Vector3(x, z, radius) of trampled ground
var river_segs := []       # [Vector2 a, Vector2 b] for layout avoidance
var stats := {}
const VS := 1.45            # village scale: structures are drawn larger than life so they read

func _init(world: WorldData, mesh_kit: MeshKit, materials: Dictionary) -> void:
	w = world
	kit = mesh_kit
	mats = materials

func build() -> Node3D:
	var t0 := Time.get_ticks_msec()
	root = Node3D.new()
	root.name = "World"
	var tb := TerrainBuilder.new(w)
	var terrain := Node3D.new()
	terrain.name = "Terrain"
	root.add_child(terrain)
	tb.build_terrain(terrain, mats.terrain)
	var t1 := Time.get_ticks_msec()

	var water := MeshInstance3D.new()
	var pm := PlaneMesh.new()
	pm.size = Vector2(w.width + 400, w.height * WorldData.S + 400)
	water.mesh = pm
	water.position = Vector3(w.width * 0.5, 0.0, w.height * WorldData.S * 0.5)
	water.material_override = mats.water
	water.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	water.name = "Water"
	root.add_child(water)
	# dark sea floor far outside the map so the horizon is not a void
	var floor := MeshInstance3D.new()
	var fm := PlaneMesh.new()
	fm.size = pm.size
	floor.mesh = fm
	floor.position = water.position + Vector3(0, -0.75, 0)
	var fmat := StandardMaterial3D.new()
	fmat.albedo_color = Color("#22495a")
	fmat.roughness = 1.0
	floor.material_override = fmat
	floor.name = "SeaFloor"
	root.add_child(floor)

	var rivers := MeshInstance3D.new()
	tb.compute_river_paths()
	rivers.mesh = tb.build_rivers()
	rivers.material_override = mats.river
	rivers.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	rivers.name = "Rivers"
	root.add_child(rivers)
	_collect_river_segments(tb.river_paths)

	var borders := MeshInstance3D.new()
	borders.mesh = tb.build_borders(1.0)
	borders.material_override = mats.border
	borders.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	borders.name = "Borders"
	root.add_child(borders)
	var borders_far := MeshInstance3D.new()
	borders_far.mesh = tb.build_borders(3.2)
	borders_far.material_override = mats.border
	borders_far.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	borders_far.name = "BordersFar"
	borders_far.visible = false
	root.add_child(borders_far)
	var t2 := Time.get_ticks_msec()

	_vegetation()
	var t3 := Time.get_ticks_msec()
	_settlements()
	_camps()
	_figures()
	_herds()
	var t4 := Time.get_ticks_msec()

	var props_node := Node3D.new()
	props_node.name = "Props"
	root.add_child(props_node)
	stats.instances = props.build(props_node, kit, _material_for)
	var t5 := Time.get_ticks_msec()
	stats.ms_terrain = t1 - t0
	stats.ms_rivers_borders = t2 - t1
	stats.ms_vegetation = t3 - t2
	stats.ms_people = t4 - t3
	stats.ms_multimesh = t5 - t4
	return root

func _material_for(mesh_name: String) -> Material:
	if mesh_name == "flame":
		return mats.fire
	if mesh_name.begins_with("person"):
		return mats.figure
	if mesh_name == "shadow_disc":
		return mats.shadow_disc
	if mesh_name.begins_with("banner"):
		return mats.banner
	if mesh_name.begins_with("detail_"):
		return mats.prop_wind
	if mesh_name.begins_with("conifer") or mesh_name.begins_with("broadleaf") or mesh_name.begins_with("acacia") \
			or mesh_name.begins_with("jungle") or mesh_name.begins_with("tuft") or mesh_name == "reeds" or mesh_name.begins_with("tallgrass"):
		return mats.prop_wind
	return mats.prop

static func rot_y(a: float) -> Basis:
	return Basis(Vector3.UP, a)

func _xf(p2: Vector2, y: float, a: float, s: float) -> Transform3D:
	return Transform3D(rot_y(a).scaled(Vector3(s, s, s)), Vector3(p2.x, y, p2.y))

func _tint(k: float) -> Color:
	return Color(0.92 + 0.16 * k, 0.94 + 0.12 * WorldData.hash01(int(k * 9999.0), 3), 0.92 + 0.14 * k, k)

# ------------------------------------------------------------------ rivers for layout

func _collect_river_segments(paths: Array) -> void:
	river_segs.clear()
	for path in paths:
		for j in path.size() - 1:
			var a: Vector3 = path[j]
			var b: Vector3 = path[j + 1]
			river_segs.append([Vector2(a.x, a.z), Vector2(b.x, b.z)])

func _river_dist(p: Vector2) -> float:
	var best := 1e9
	for s in river_segs:
		var a: Vector2 = s[0]
		if a.distance_squared_to(p) > 4.0:
			continue
		var q := Geometry2D.get_closest_point_to_segment(p, a, s[1])
		best = minf(best, q.distance_to(p))
	return best

# ------------------------------------------------------------------ vegetation

func _rand_in_hex(t: int, k: int, r: float) -> Vector2:
	var xy := w.tile_xy(t)
	var c := w.tile_center(t)
	for attempt in 6:
		var u := WorldData.hash01(xy.x * 31 + k, xy.y * 17 + attempt, 101) * 2.0 - 1.0
		var v := WorldData.hash01(xy.x * 13 + k, xy.y * 29 + attempt, 202) * 2.0 - 1.0
		var p := Vector2(u * 0.5, v * WorldData.R) * r
		# inside pointy-top hex of circumradius R*r?
		var ax := absf(p.x)
		var az := absf(p.y)
		if ax <= 0.5 * r and az <= WorldData.R * r - ax * 0.57735:
			return c + p
	return c

func _vegetation() -> void:
	var B := WorldData.Biome
	var count := 0
	for t in w.width * w.height:
		var b := int(w.biome[t])
		if b <= B.COAST:
			continue
		var res := int(w.reserved[t])
		if res >= 2:
			# settlement tile or farmed plot: only a few tufts at the rim
			continue
		var h: float = w.heights[t]
		var xy := w.tile_xy(t)
		var near := res == 1
		var river := (w.flags[t] & WorldData.FLAG_RIVER) != 0
		var trees := 0
		var tree_kind := ""
		var bushes := 0
		var tufts := 0
		var rocks := 0
		var reeds := 0
		var tuft_kind := "tuft0"
		var hk := WorldData.hash01(xy.x, xy.y, 55)
		match b:
			B.ICE:
				rocks = 1 if hk > 0.6 else 0
			B.TUNDRA:
				rocks = 1 + int(hk * 2.0)
				bushes = 2
				tufts = 3
				tuft_kind = "tuft1"
			B.TAIGA:
				trees = 6
				tree_kind = "conifer"
				rocks = 1
			B.TEMPERATE_FOREST:
				trees = 7
				tree_kind = "mixed"
				bushes = 1
			B.OPEN_WOODLAND:
				trees = 3 + int(hk * 2.0)
				tree_kind = "broadleaf"
				bushes = 2
				tufts = 6
			B.GRASSLAND:
				trees = 1 if hk > 0.75 else 0
				tree_kind = "broadleaf"
				bushes = 1
				tufts = 8
			B.SAVANNA:
				trees = 2 + (1 if hk > 0.5 else 0)
				tree_kind = "acacia"
				bushes = 1 + int(hk * 2.0)
				tufts = 10
				tuft_kind = "tuft1"
			B.TROPICAL_FOREST:
				trees = 6
				tree_kind = "jungle"
				bushes = 2
			B.WETLAND:
				reeds = 6
				bushes = 2
				tufts = 3
			B.DESERT:
				rocks = 2
				bushes = 1 if hk > 0.5 else 0
				tuft_kind = "tuft1"
				tufts = 2
			B.MOUNTAIN, B.PEAK:
				var mi := (3 if b == B.PEAK else 0) + int(hk * 2.999)
				var s := 0.75 + 0.45 * WorldData.hash01(xy.x, xy.y, 57)
				var sy := 0.6 + 0.8 * WorldData.hash01(xy.x, xy.y, 58)
				var off := Vector2(WorldData.hash01(xy.x, xy.y, 59) - 0.5, WorldData.hash01(xy.x, xy.y, 60) - 0.5) * 0.36
				var mc := w.tile_center(t) + off
				var xf := Transform3D(rot_y(hk * TAU).scaled(Vector3(s, s * sy, s)), Vector3(mc.x, h - 0.03, mc.y))
				props.add("mountain%d" % mi, xf, Color(1, 1, 1, hk))
				if WorldData.hash01(xy.x, xy.y, 61) > 0.6:
					# a smaller satellite peak breaks the one-peak-per-hex rhythm
					var mc2 := w.tile_center(t) - off * 1.4
					var s2 := s * 0.55
					props.add("mountain%d" % ((mi + 1) % 6), Transform3D(rot_y(hk * 5.0).scaled(Vector3(s2, s2 * sy, s2)), Vector3(mc2.x, h - 0.03, mc2.y)), Color(1, 1, 1, hk))
				rocks = 1
				if b == B.MOUNTAIN and hk < 0.5:
					trees = 2
					tree_kind = "conifer"
		var grove := _grove(xy.x, xy.y)
		if tree_kind == "acacia" or tree_kind == "broadleaf":
			trees = int(round(trees * (0.1 + 2.0 * grove * grove)))
			tufts += int(4.0 * (1.0 - grove))
		elif trees > 0:
			trees = int(round(trees * (0.7 + 0.6 * grove)))
		if near:
			trees = trees / 2
		if river:
			trees = trees * 2 / 3
			reeds += 2
		if w.flags[t] & WorldData.FLAG_COAST_ADJ:
			reeds += 1 if b != B.DESERT else 0
		var k := 0
		var c := w.tile_center(t)
		for i in trees:
			var p := _rand_in_hex(t, k, 0.85)
			k += 1
			if river and _river_dist(p) < 0.12:
				continue
			if near and _near_settlement(p, 0.55):
				continue
			var name := ""
			var hv := WorldData.hash01(xy.x * 7 + i, xy.y, 61)
			match tree_kind:
				"conifer": name = "conifer%d" % int(hv * 3.0)
				"broadleaf": name = "broadleaf%d" % int(hv * 3.0)
				"acacia": name = "acacia%d" % int(hv * 2.0)
				"jungle": name = ("jungle%d" % int(hv * 2.0)) if hv < 0.75 else "broadleaf%d" % int(hv * 3.0 - 2.0)
				"mixed": name = ("conifer%d" % int(hv * 3.0)) if WorldData.hash01(xy.x, xy.y * 3 + i, 62) < 0.45 else "broadleaf%d" % int(hv * 3.0)
			var s := 0.8 + 0.45 * WorldData.hash01(xy.x, xy.y + i, 63)
			var y := w.height_at(p)
			props.add(name, _xf(p, y, hv * TAU, s), _tint(WorldData.hash01(xy.x + i, xy.y, 64)))
			count += 1
		for i in bushes:
			var p := _rand_in_hex(t, k, 0.9)
			k += 1
			if river and _river_dist(p) < 0.1:
				continue
			var hv := WorldData.hash01(xy.x * 5 + i, xy.y, 71)
			var nm := "shrub" if (b == B.TUNDRA or b == B.DESERT or b == B.SAVANNA) and hv > 0.4 else "bush"
			props.add(nm, _xf(p, w.height_at(p), hv * TAU, 0.8 + 0.5 * hv), _tint(hv))
		for i in tufts:
			var p := _rand_in_hex(t, k, 0.95)
			k += 1
			var hv := WorldData.hash01(xy.x * 3 + i, xy.y, 81)
			var nm := tuft_kind
			if (b == B.SAVANNA or b == B.GRASSLAND or b == B.OPEN_WOODLAND) and hv > 0.45:
				nm = "tallgrass0" if b == B.SAVANNA else "tallgrass1"
			props.add(nm, _xf(p, w.height_at(p), hv * TAU, 0.8 + 0.6 * hv), _tint(hv))
		# close-range detail grass (faded out beyond ~30 units)
		var detail := 0
		var dname := ""
		match b:
			B.SAVANNA: 
				detail = 16
				dname = "detail_tallgrass0"
			B.GRASSLAND, B.OPEN_WOODLAND:
				detail = 14
				dname = "detail_tallgrass1"
			B.TEMPERATE_FOREST, B.TROPICAL_FOREST, B.TAIGA, B.WETLAND, B.TUNDRA:
				detail = 6
				dname = "detail_tuft0"
		for i in detail:
			var p := _rand_in_hex(t, 200 + i, 0.98)
			if river and _river_dist(p) < 0.1:
				continue
			var hv := WorldData.hash01(xy.x * 17 + i, xy.y, 83)
			var cl := _grove(int(p.x * 3.0), int(p.y * 3.0))
			if hv > 0.35 + 0.6 * cl:
				continue
			props.add(dname, _xf(p, w.height_at(p), hv * TAU, 0.6 + 0.7 * hv), _tint(hv))
		for i in rocks:
			var p := _rand_in_hex(t, k, 0.8)
			k += 1
			var hv := WorldData.hash01(xy.x * 9 + i, xy.y, 91)
			props.add("rock%d" % int(hv * 3.0), _xf(p, w.height_at(p) - 0.01, hv * TAU, 0.7 + 0.8 * hv), Color(1, 1, 1, hv))
		for i in reeds:
			var p: Vector2
			if river:
				# along the river banks
				p = _rand_in_hex(t, k, 0.6)
			else:
				p = _shore_point(t, k)
			k += 1
			var hv := WorldData.hash01(xy.x * 11 + i, xy.y, 95)
			props.add("reeds", _xf(p, w.height_at(p), hv * TAU, 0.8 + 0.4 * hv), _tint(hv))
	stats.trees = count

## Smooth value noise over tiles (0..1): groves and clearings.
func _grove(x: int, y: int) -> float:
	var fx := x / 3.7
	var fy := y / 3.7
	var ix := int(floor(fx))
	var iy := int(floor(fy))
	var ux := fx - ix
	var uy := fy - iy
	ux = ux * ux * (3.0 - 2.0 * ux)
	uy = uy * uy * (3.0 - 2.0 * uy)
	var a := WorldData.hash01(ix, iy, 777)
	var b := WorldData.hash01(ix + 1, iy, 777)
	var c := WorldData.hash01(ix, iy + 1, 777)
	var d := WorldData.hash01(ix + 1, iy + 1, 777)
	return lerpf(lerpf(a, b, ux), lerpf(c, d, ux), uy)

## A point near the edge of t that faces water.
func _shore_point(t: int, k: int) -> Vector2:
	var c := w.tile_center(t)
	for i in 6:
		var e := (i + k) % 6
		var nb := w.neighbour(t, e)
		if nb >= 0 and w.is_water(nb):
			var a: Vector2 = WorldData.corner(e)
			var b: Vector2 = WorldData.corner((e + 1) % 6)
			var f := WorldData.hash01(t, k, 97)
			return c + (a.lerp(b, 0.2 + 0.6 * f)) * 0.86
	return _rand_in_hex(t, k, 0.9)

func _near_settlement(p: Vector2, r: float) -> bool:
	for s in w.settlements:
		if w.tile_center(int(s.tile)).distance_to(p) < r:
			return true
	return false

# ------------------------------------------------------------------ settlements

func _ok_site(p: Vector2, h0: float) -> bool:
	var t := w.tile_at(p)
	if t < 0 or not w.is_land(t):
		return false
	if absf(w.heights[t] - h0) > 0.06:
		return false
	if w.plot_kind[t] >= 0:
		return false
	if w.biome[t] >= WorldData.Biome.MOUNTAIN:
		return false
	if _river_dist(p) < 0.11:
		return false
	return true

func _settlements() -> void:
	var K := WorldData.Kind
	hearths.clear()
	# fields and orchards on their plot tiles
	for pl in w.plots:
		var t := int(pl[0])
		if not w.is_land(t):
			continue
		var c := w.tile_center(t)
		var xy := w.tile_xy(t)
		var hv := WorldData.hash01(xy.x, xy.y, 301)
		var h: float = w.heights[t]
		if int(pl[1]) == 0:
			props.add("field", _xf(c, h, PI / 3.0 * int(hv * 3.0) + PI / 6.0 * 0.0, 1.0), Color(1, 1, 1, hv))
		else:
			for i in 7:
				var p := c if i == 6 else c + Vector2(cos(TAU * i / 6.0), sin(TAU * i / 6.0)) * 0.27
				var hv2 := WorldData.hash01(xy.x + i, xy.y, 303)
				props.add("orchard_tree", _xf(p, h, hv2 * TAU, 0.9 + 0.3 * hv2), _tint(hv2))
			for i in 6:
				var p := _rand_in_hex(t, 40 + i, 0.9)
				props.add("tuft0", _xf(p, h, i * 1.3, 1.0), _tint(0.5))

	for si in w.settlements.size():
		var s: Dictionary = w.settlements[si]
		var t := int(s.tile)
		var civ := int(s.civ)
		var st: Array = s.structures
		var cc := MeshBuilder.lin(WorldData.civ_color(civ), 0.5)
		var h0: float = w.heights[t]
		var c0 := w.tile_center(t)
		# hearth: the tile centre, or nudged off a river
		var c := c0
		if _river_dist(c0) < 0.14:
			for i in 12:
				var a := TAU * i / 12.0
				var q := c0 + Vector2(cos(a), sin(a)) * 0.2
				if _ok_site(q, h0) and _river_dist(q) > 0.16:
					c = q
					break
		var seed := float(t % 997) / 997.0
		hearths.append(Vector3(c.x, h0, c.y))
		if st[K.HEARTH] > 0:
			props.add("hearth", _xf(c, h0, seed * TAU, VS), Color(1, 1, 1, seed), false)
			props.add("flame", _xf(c, h0 + 0.004, seed * TAU, VS), Color(1, 1, 1, seed), false)
		props.add("banner%d" % civ, _xf(c + Vector2(0.09, -0.07), h0, -0.4, VS), Color(cc.r, cc.g, cc.b, seed), false)

		# items to lay out on a sunflower spiral around the hearth
		var items := []
		var dwell_kinds := [[K.LEAN_TO, "leanto"], [K.HIDE_TENT, "tent"], [K.PIT_HOUSE, "pithouse"],
			[K.WATTLE_HOUSE, "wattle"], [K.MUDBRICK_HOUSE, "mudbrick"], [K.LONGHOUSE, "longhouse"]]
		var present := []
		var dsum := 0
		for dk in dwell_kinds:
			if int(st[dk[0]]) > 0:
				present.append(dk)
				dsum += int(st[dk[0]])
		if present.is_empty():
			present.append([K.LEAN_TO, "leanto"])
		# households scale with population; the kinds come from the structure counts
		var households := maxi(dsum, int(ceil(float(s.population) / 5.0)))
		for i in households:
			# better houses first (they are the most recent tech), weighted by count
			var dk = present[present.size() - 1 - (i % present.size())]
			items.append(dk[1])
		var extras := [[K.STORAGE_PIT, "storagepit", 3], [K.DRYING_RACK, "dryingrack", 2], [K.GRANARY, "granary", 2],
			[K.KILN, "kiln", 1], [K.QUERN, "quern", 2]]
		var ins := 2
		for ex in extras:
			var n := mini(int(st[ex[0]]), 4)
			if n > 0:
				n = maxi(n, ex[2])
			for j in n:
				items.insert(mini(ins, items.size()), ex[1])
				ins += 3
		var placed := []
		var slot := 0
		var rmax := 0.0
		for it in items:
			var p := Vector2.ZERO
			var found := false
			while slot < 80:
				var a := slot * 2.39996 + seed * TAU
				var r := 0.112 * VS * sqrt(slot + 3.2)
				slot += 1
				var q := c + Vector2(cos(a), sin(a)) * r
				if not _ok_site(q, h0):
					continue
				var clear := true
				for pp in placed:
					if (pp as Vector2).distance_to(q) < 0.15 * VS:
						clear = false
						break
				if not clear:
					continue
				p = q
				found = true
				break
			if not found:
				break
			placed.append(p)
			rmax = maxf(rmax, p.distance_to(c))
			var d := c - p
			var ang := atan2(d.x, d.y)
			var sc := VS * (0.95 + 0.15 * WorldData.hash01(t, placed.size(), 5))
			props.add(it, _xf(p, w.height_at(p), ang, sc), Color(cc.r, cc.g, cc.b, WorldData.hash01(t, placed.size(), 6)), false)
		villages.append(Vector3(c.x, c.y, rmax * 0.75 + 0.08))
		# lookout tower with a big banner at the edge
		if st[K.LOOKOUT] > 0:
			for i in 12:
				var a := TAU * i / 12.0 + seed * 3.0
				var q := c + Vector2(cos(a), sin(a)) * (rmax + 0.12)
				if _ok_site(q, h0):
					props.add("lookout", _xf(q, w.height_at(q), a, VS), Color(cc.r, cc.g, cc.b, seed), false)
					props.add("banner_small%d" % civ, _xf(q, w.height_at(q) + 0.285 * VS, -0.4, VS), Color(cc.r, cc.g, cc.b, seed), false)
					break
		# thorn barrier ring with a gate
		if st[K.THORN_BARRIER] > 0:
			var ring := rmax + 0.13
			var nseg := int(ceil(TAU * ring / (0.11 * 1.2)))
			var gate := int(seed * nseg)
			for i in nseg:
				if i == gate or i == (gate + 1) % nseg:
					continue
				var a := TAU * (i + 0.5) / nseg
				var q := c + Vector2(cos(a), sin(a)) * ring * (1.0 + 0.08 * sin(a * 3.0 + seed * 7.0) + 0.04 * (WorldData.hash01(t, i, 7) - 0.5))
				if WorldData.hash01(t, i, 10) < 0.04:
					continue
				var tq := w.tile_at(q)
				if tq < 0 or not w.is_land(tq) or _river_dist(q) < 0.08:
					continue
				var tang := a + PI * 0.5
				props.add("thorn", _xf(q, w.height_at(q), -tang, 1.2 + 0.1 * WorldData.hash01(t, i, 8)), Color(1, 1, 1, WorldData.hash01(t, i, 9)), false)
		# weir in the nearest water
		if st[K.WEIR] > 0:
			for i in 6:
				var nb := w.neighbour(t, i)
				if nb >= 0 and w.is_water(nb):
					var q := (c0 + w.tile_center(nb)) * 0.5 + (w.tile_center(nb) - c0) * 0.12
					props.add("weir", _xf(q, -0.02, atan2((w.tile_center(nb) - c0).x, (w.tile_center(nb) - c0).y), 1.0), Color(1, 1, 1, seed), false)
					break
			# a weir across the river where it passes the village
			var best := 1e9
			var bq := Vector2.ZERO
			var bd := Vector2.ZERO
			for sgi in river_segs:
				var a2: Vector2 = sgi[0]
				if a2.distance_to(c) > 0.9:
					continue
				var dd := absf(a2.distance_to(c) - 0.42)
				if dd < best:
					best = dd
					bq = a2
					bd = (sgi[1] as Vector2) - a2
			if best < 0.3:
				props.add("weir", _xf(bq, w.height_at(bq) + 0.02, atan2(bd.x, bd.y), 0.55), Color(1, 1, 1, seed), false)
		# pasture: a fenced paddock with a few sheep on a free neighbour tile
		if st[K.PASTURE] > 0:
			for i in 6:
				var nb := w.neighbour(t, (i + int(seed * 6.0)) % 6)
				if nb < 0 or not w.is_land(nb) or w.plot_kind[nb] >= 0 or w.biome[nb] >= WorldData.Biome.MOUNTAIN:
					continue
				if w.flags[nb] & WorldData.FLAG_RIVER:
					continue
				var pc := w.tile_center(nb)
				var ph: float = w.heights[nb]
				var pr := 0.3
				for e in 6:
					var a0 := WorldData.corner(e) / WorldData.R * pr
					var a1 := WorldData.corner((e + 1) % 6) / WorldData.R * pr
					for half in 2:
						var q := pc + a0.lerp(a1, 0.25 + 0.5 * half)
						var dd := a1 - a0
						props.add("fence", _xf(q, ph, -atan2(dd.y, dd.x), 1.15), Color(1, 1, 1, 0.5), false)
				for j in 4:
					var q := pc + Vector2(cos(j * 1.7 + seed), sin(j * 1.7 + seed)) * (0.06 + 0.04 * j)
					var col := MeshBuilder.lin(Color("#e8dfcc") if j != 2 else Color("#6e5a48"), 0.5)
					props.add("animal_woolly", _xf(q, ph, j * 2.1 + seed, 1.0), Color(col.r, col.g, col.b, 0.5), false)
				break

func _camps() -> void:
	for cp in w.camps:
		var t := int(cp[0])
		var civ := int(cp[1])
		if not w.is_land(t):
			continue
		var c := w.tile_center(t) + Vector2(0.12, 0.05)
		var h: float = w.heights[t]
		var cc := MeshBuilder.lin(WorldData.civ_color(civ), 0.5)
		var seed := float(t % 113) / 113.0
		villages.append(Vector3(c.x + 0.05, c.y + 0.05, 0.12))
		props.add("camp", _xf(c, h, seed * TAU, 1.0), Color(cc.r, cc.g, cc.b, seed), false)
		props.add("camp", _xf(c + Vector2(0.1, 0.06), h, seed * TAU + 2.0, 0.85), Color(cc.r, cc.g, cc.b, seed), false)
		props.add("hearth", _xf(c + Vector2(0.03, 0.12), h, seed, 0.7), Color(1, 1, 1, seed), false)
		props.add("flame", _xf(c + Vector2(0.03, 0.12), h + 0.003, seed, 0.6), Color(1, 1, 1, seed), false)
		props.add("banner_small%d" % civ, _xf(c + Vector2(-0.07, -0.02), h, -0.4, 1.0), Color(cc.r, cc.g, cc.b, seed), false)

# ------------------------------------------------------------------ people

const TOOL_BY_ACTIVITY := {
	0: "plain", 1: "basket", 2: "spear", 3: "pole", 4: "hoe", 5: "axe", 6: "basket", 7: "hammer", 8: "hammer",
	9: "plain", 10: "plain", 11: "plain", 12: "plain", 15: "bundle", 16: "bundle", 17: "spear", 18: "spear", 19: "spear",
}

func _figures() -> void:
	var n := 0
	for f in w.figures:
		var p := w.map_to_world2(float(f[0]), float(f[1]))
		var civ := int(f[2])
		var age := int(f[3])
		var act := int(f[4])
		var fl := int(f[5])
		var home := int(f[7])
		var seed := WorldData.hash01(int(f[0]), int(f[1]), n)
		var face := seed * TAU
		# people at home gather around the hearth (the sim keeps them within ~0.3 tiles of the centre)
		if home >= 0 and home < hearths.size():
			var hc: Vector3 = hearths[home]
			var h2 := Vector2(hc.x, hc.z)
			var d := p - h2
			if d.length() < 0.45:
				var r := d.length()
				var dir := d / r if r > 0.0001 else Vector2(cos(face), sin(face))
				var nr := 0.07 + r * 0.55
				p = h2 + dir * nr
				# keep people out of the river that runs past the hearth
				var tries := 0
				while _river_dist(p) < 0.07 and tries < 12:
					dir = dir.rotated(0.5)
					p = h2 + dir * nr
					tries += 1
				var to := h2 - p
				face = atan2(to.x, to.y) + (seed - 0.5) * 1.2
		var mesh := "person_plain"
		if act == 14:
			mesh = "person_lying"
		elif age == 0 or act == 13:
			mesh = "person_child"
		elif age == 2:
			mesh = "person_elder"
		else:
			mesh = "person_" + str(TOOL_BY_ACTIVITY.get(act, "plain"))
			if fl & 256:
				mesh = "person_spear"
		var y := maxf(w.height_at(p), 0.0)
		var cc := MeshBuilder.lin(WorldData.civ_color(civ) if not (fl & 1024) else Color("#9a9a9a"), 0.5)
		props.add(mesh, _xf(p, y, face, 1.0), Color(cc.r, cc.g, cc.b, seed), false)
		props.add("shadow_disc", _xf(p, y + 0.001, 0.0, 1.0), Color(1, 1, 1, seed), false)
		n += 1
	stats.figures = n

func _herds() -> void:
	var n := 0
	for hd in w.herds:
		var p := w.map_to_world2(float(hd[0]), float(hd[1]))
		var sp := clampi(int(hd[2]), 0, SPECIES.size() - 1)
		var animals := int(hd[3])
		var cfg: Array = SPECIES[sp]
		var cnt := clampi(int(round(animals / 7.0)), 2, 7)
		var heading := WorldData.hash01(int(hd[0]), int(hd[1]), 1) * TAU
		for i in cnt:
			var a := i * 2.39996 + heading
			var r := 0.055 * sqrt(i + 0.5) * float(cfg[2])
			var q := p + Vector2(cos(a), sin(a)) * r
			var t := w.tile_at(q)
			if t < 0 or not w.is_land(t):
				continue
			var hv := WorldData.hash01(int(hd[0]) + i, int(hd[1]), 2)
			var col := MeshBuilder.lin(Color(cfg[1]).lightened(0.12 * (hv - 0.5)), 0.5)
			props.add(cfg[0], _xf(q, w.heights[t], heading + (hv - 0.5) * 1.4, float(cfg[2]) * (0.85 + 0.25 * hv)), Color(col.r, col.g, col.b, hv), false)
			n += 1
	stats.animals = n
