## Turns a M7WorldData frame into a diorama: terrain, water, rivers, borders, vegetation,
## settlements (procedural layout from structure counts), camps, people and herds.
class_name M7WorldBuilder
extends RefCounted

const RIVER_GRID_SIZE := 4.0
const RIVER_SEARCH_RADIUS := 2.0
const STATIC_CHUNK_SIZE := 40.0
const STATIC_DETAIL_CHUNK_SIZE := 12.0

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

var w: M7WorldData
var kit: M7MeshKit
var mats: Dictionary
var props := M7Batcher.new()
var root: Node3D
var terrain_builder: M7TerrainBuilder
var settlement_props: Node3D
var actor_props: Node3D
var vegetation_props: Node3D
var actor_multimeshes := {}
var herd_props: Node3D
var herd_multimeshes := {}
var herds_dirty := false   # herds and beacons rebuild one frame after people, so a publish never costs two frames' worth
var border_node: MeshInstance3D
var borders_far_node: MeshInstance3D
var river_node: MeshInstance3D
var structure_signature := -1
var rig: M7MonumentRig
var boat_probe := Vector2.ZERO          # the last boat placed (dev scenarios aim the camera at it)
var trail_node: MeshInstance3D
var trail_signature := -1
var trail_polys: Array = []              # centre lines of the trail ribbons (trees are cleared along them)
var trails_changed := false
var border_signature := -1
var river_visibility_signature := -1
var regional_lod := false
var figure_tints := PackedColorArray()
var freefolk_tint: Color
var hearths := []          # world positions of settlement hearths (index = settlement)
var villages := []         # Vector3(x, z, radius) of trampled ground
var tree_points := PackedVector2Array()   # where full-size trees stand (the megalith ring keeps clear of them)
var river_segs := []       # [Vector2 a, Vector2 b] for layout avoidance
var river_grid := {}       # 4-unit cells to nearby river segment indices
var stats := {}
const VS := 1.45            # village scale: structures are drawn larger than life so they read

func _init(world: M7WorldData, mesh_kit: M7MeshKit, materials: Dictionary) -> void:
	w = world
	kit = mesh_kit
	mats = materials
	for civ in 8:
		var colour := M7WorldData.civ_color(civ).srgb_to_linear()
		figure_tints.append(Color(colour.r, colour.g, colour.b, 1.0))
	var freefolk := Color("#9a9a9a").srgb_to_linear()
	freefolk_tint = Color(freefolk.r, freefolk.g, freefolk.b, 1.0)

func build() -> Node3D:
	var t0 := Time.get_ticks_msec()
	root = Node3D.new()
	root.name = "World"
	terrain_builder = M7TerrainBuilder.new(w)
	var terrain := Node3D.new()
	terrain.name = "Terrain"
	root.add_child(terrain)
	terrain_builder.build_terrain(terrain, mats.terrain)
	var t1 := Time.get_ticks_msec()

	var water := MeshInstance3D.new()
	var pm := PlaneMesh.new()
	pm.size = Vector2(w.width + 400, w.height * M7WorldData.S + 400)
	water.mesh = pm
	water.position = Vector3(w.width * 0.5, 0.0, w.height * M7WorldData.S * 0.5)
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
	terrain_builder.compute_river_paths()
	rivers.mesh = terrain_builder.build_rivers()
	rivers.material_override = mats.river
	rivers.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	rivers.name = "Rivers"
	root.add_child(rivers)
	river_node = rivers
	_collect_river_segments(terrain_builder.river_paths)

	var borders := MeshInstance3D.new()
	borders.mesh = terrain_builder.build_borders(1.0)
	borders.material_override = mats.border
	borders.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	borders.name = "Borders"
	root.add_child(borders)
	border_node = borders
	var borders_far := MeshInstance3D.new()
	borders_far.mesh = terrain_builder.build_borders(3.2)
	borders_far.material_override = mats.border
	borders_far.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	borders_far.name = "BordersFar"
	borders_far.visible = false
	root.add_child(borders_far)
	borders_far_node = borders_far
	border_signature = _border_digest()
	river_visibility_signature = _visibility_digest()
	var t2 := Time.get_ticks_msec()

	props = M7Batcher.new()
	props.chunk_size = STATIC_CHUNK_SIZE
	props.detail_chunk_size = STATIC_DETAIL_CHUNK_SIZE
	_vegetation()
	vegetation_props = _build_props_node("Vegetation", props)
	props = M7Batcher.new()
	props.chunk_size = STATIC_CHUNK_SIZE
	props.detail_chunk_size = STATIC_DETAIL_CHUNK_SIZE
	var t3 := Time.get_ticks_msec()
	_settlements()
	_camps()
	settlement_props = _build_props_node("SettlementProps", props)
	structure_signature = _structure_digest()
	rig = M7MonumentRig.new(self)
	rig.set_monuments(w.monuments, structure_signature)
	trail_node = MeshInstance3D.new()
	trail_node.name = "Trails"
	trail_node.material_override = M7TrailBuilder.make_material()
	trail_node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	root.add_child(trail_node)
	_update_trails()
	props = M7Batcher.new()
	_figures()
	actor_props = _build_props_node("Actors", props)
	props = M7Batcher.new()
	_herds()
	_beacons()
	herd_props = _build_props_node("Herds", props)
	props = M7Batcher.new()
	var t4 := Time.get_ticks_msec()
	var t5 := Time.get_ticks_msec()
	stats.instances = stats.get("instances_vegetation", 0) + stats.get("instances_settlements", 0) + stats.get("instances_actors", 0)
	stats.ms_terrain = t1 - t0
	stats.ms_rivers_borders = t2 - t1
	stats.ms_vegetation = t3 - t2
	stats.ms_people = t4 - t3
	stats.ms_multimesh = t5 - t4
	return root

## Apply a newly published MatchFrame without rebuilding the terrain or vegetation.
func update_frame() -> bool:
	var started := Time.get_ticks_usec()
	var new_river_signature := _visibility_digest()
	if new_river_signature != river_visibility_signature:
		river_visibility_signature = new_river_signature
		terrain_builder.compute_river_paths()
		river_node.mesh = terrain_builder.build_rivers()
		_collect_river_segments(terrain_builder.river_paths)
	var new_border_signature := _border_digest()
	if new_border_signature != border_signature:
		border_signature = new_border_signature
		border_node.mesh = terrain_builder.build_borders(1.0)
		borders_far_node.mesh = terrain_builder.build_borders(3.2)
	_update_trails()
	var geometry_updated := Time.get_ticks_usec()
	var signature := _structure_digest()
	var structures_changed := signature != structure_signature
	if structures_changed:
		structure_signature = signature
		hearths.clear()
		villages.clear()
		props = M7Batcher.new()
		props.chunk_size = STATIC_CHUNK_SIZE
		props.detail_chunk_size = STATIC_DETAIL_CHUNK_SIZE
		_settlements()
		_camps()
		_replace_props_node(settlement_props, "SettlementProps", props)
		props = M7Batcher.new()
	rig.set_monuments(w.monuments, structure_signature)
	var settlements_updated := Time.get_ticks_usec()
	props = M7Batcher.new()
	var people_started := Time.get_ticks_usec()
	_figures()
	var people_finished := Time.get_ticks_usec()
	var herds_finished := people_finished
	var actors_batched := Time.get_ticks_usec()
	actor_multimeshes = props.update_existing(actor_props, kit, _material_for, actor_multimeshes)
	var actors_uploaded := Time.get_ticks_usec()
	props = M7Batcher.new()
	herds_dirty = true
	if "--perf-debug" in OS.get_cmdline_user_args():
		print("M7A_FRAME_SCENE geometry=", (geometry_updated - started) / 1000.0,
			" settlements=", (settlements_updated - geometry_updated) / 1000.0,
			" people=", (people_finished - people_started) / 1000.0,
			" herds=", (herds_finished - people_finished) / 1000.0,
			" actor_build=", (actors_batched - settlements_updated) / 1000.0,
			" actor_upload=", (actors_uploaded - actors_batched) / 1000.0,
			" actor_groups=", actor_multimeshes.size(),
			" structure_changed=", structures_changed)
	return structures_changed

## Swap vegetation meshes and use compact actor silhouettes at regional zoom.
## Mesh swaps retain the existing MultiMesh buffers and draw-call grouping.
func set_regional_lod(enabled: bool) -> void:
	if regional_lod == enabled or vegetation_props == null:
		return
	regional_lod = enabled
	for child in vegetation_props.get_children():
		if not child is MultiMeshInstance3D:
			continue
		var instance := child as MultiMeshInstance3D
		var mesh_name := String(instance.get_meta("civsim_mesh_name", ""))
		var far_mesh := kit.get_far_tree_mesh(mesh_name)
		if far_mesh != null:
			instance.multimesh.mesh = far_mesh if enabled else kit.get_mesh(mesh_name)
			instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF if enabled \
				else GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	props = M7Batcher.new()
	_figures()
	actor_multimeshes = props.update_existing(actor_props, kit, _material_for, actor_multimeshes)
	props = M7Batcher.new()
	herds_dirty = true
	update_herds()

## Rebuilds herds and beacons; called a frame after the people so the work is split across two frames.
func update_herds() -> void:
	if not herds_dirty:
		return
	herds_dirty = false
	var started := Time.get_ticks_usec()
	props = M7Batcher.new()
	_herds()
	_beacons()
	herd_multimeshes = props.update_existing(herd_props, kit, _material_for, herd_multimeshes)
	props = M7Batcher.new()
	if "--perf-debug" in OS.get_cmdline_user_args():
		print("M7A_FRAME_HERDS ms=", (Time.get_ticks_usec() - started) / 1000.0)

func _build_props_node(node_name: String, batcher: M7Batcher) -> Node3D:
	var node := Node3D.new()
	node.name = node_name
	root.add_child(node)
	var count: int
	if node_name == "Actors":
		actor_multimeshes = batcher.update_existing(node, kit, _material_for, {})
		count = batcher.instance_count
	elif node_name == "Herds":
		herd_multimeshes = batcher.update_existing(node, kit, _material_for, {})
		count = batcher.instance_count
	else:
		count = batcher.build(node, kit, _material_for)
	stats["instances_" + node_name.to_lower().trim_suffix("props").trim_suffix("s")] = count
	stats["multimesh_groups_" + node_name.to_lower()] = batcher.groups.size()
	return node

func _replace_props_node(previous: Node3D, node_name: String, batcher: M7Batcher) -> void:
	if is_instance_valid(previous):
		previous.queue_free()
	var node := _build_props_node(node_name, batcher)
	if node_name == "SettlementProps":
		settlement_props = node
	else:
		actor_props = node

## Rebuilds the trail ribbons when the routes, their wear or what is explored along them changes.
func _update_trails() -> void:
	var sig := hash(w.trail_meta) * 31 + hash(w.trail_tiles)
	if sig == trail_signature:
		return
	trail_signature = sig
	trail_polys = []
	trail_node.mesh = M7TrailBuilder.build(w, trail_polys)
	trails_changed = true

func _structure_digest() -> int:
	var h := 17
	for s in w.settlements:
		h = h * 31 + int(s.tile)
		h = h * 31 + int(s.civ)
		h = h * 31 + int(s.population / 8)           # the fleet's size steps with it
		h = h * 31 + (w.sea_tier[int(s.civ)] if int(s.civ) >= 0 and int(s.civ) < w.sea_tier.size() else 0)
		for n in s.structures:
			h = h * 31 + int(n)
		for n in s.get("decor", PackedInt32Array()):
			h = h * 31 + int(n) + 1
	for i in range(0, w.plots.size(), 2):
		h = h * 31 + w.plots[i] * 7 + w.plots[i + 1]
	for c in w.camps:
		h = h * 31 + int(c[0]) * 7 + int(c[1])
	return h

func _border_digest() -> int:
	var h := 0
	for t in w.width * w.height:
		h = int((h * 65599 + (int(w.owner[t]) + 2) * 7 + int(w.visibility[t])) & 0x7fffffff)
	return h

func _visibility_digest() -> int:
	var h := 0
	for v in w.visibility:
		h = int((h * 65599 + int(v) + 1) & 0x7fffffff)
	return h

func _material_for(mesh_name: String) -> Material:
	if mesh_name == "flame":
		return mats.fire
	if mesh_name.begins_with("person"):
		return mats.figure
	if mesh_name == "shadow_disc":
		return mats.shadow_disc
	if mesh_name.begins_with("animal_"):
		return mats.animal
	if mesh_name.begins_with("banner"):
		return mats.banner
	if mesh_name.begins_with("detail_") or mesh_name.begins_with("tuft") or mesh_name == "reeds" or mesh_name.begins_with("tallgrass"):
		return mats.grass_wind
	if mesh_name.begins_with("conifer") or mesh_name.begins_with("broadleaf") or mesh_name.begins_with("acacia") \
			or mesh_name.begins_with("jungle"):
		return mats.prop_wind
	return mats.prop

static func rot_y(a: float) -> Basis:
	return Basis(Vector3.UP, a)

func _xf(p2: Vector2, y: float, a: float, s: float) -> Transform3D:
	return Transform3D(rot_y(a).scaled(Vector3(s, s, s)), Vector3(p2.x, y, p2.y))

func _tint(k: float) -> Color:
	return Color(0.92 + 0.16 * k, 0.94 + 0.12 * M7WorldData.hash01(int(k * 9999.0), 3), 0.92 + 0.14 * k, k)

# ------------------------------------------------------------------ rivers for layout

func _collect_river_segments(paths: Array) -> void:
	river_segs.clear()
	river_grid.clear()
	for path in paths:
		for j in path.size() - 1:
			var a: Vector3 = path[j]
			var b: Vector3 = path[j + 1]
			river_segs.append([Vector2(a.x, a.z), Vector2(b.x, b.z)])
			var segment_index: int = river_segs.size() - 1
			var min_x := int(floor((minf(a.x, b.x) - RIVER_SEARCH_RADIUS) / RIVER_GRID_SIZE))
			var max_x := int(floor((maxf(a.x, b.x) + RIVER_SEARCH_RADIUS) / RIVER_GRID_SIZE))
			var min_z := int(floor((minf(a.z, b.z) - RIVER_SEARCH_RADIUS) / RIVER_GRID_SIZE))
			var max_z := int(floor((maxf(a.z, b.z) + RIVER_SEARCH_RADIUS) / RIVER_GRID_SIZE))
			for gz in range(min_z, max_z + 1):
				for gx in range(min_x, max_x + 1):
					var key := Vector2i(gx, gz)
					if not river_grid.has(key):
						river_grid[key] = []
					river_grid[key].append(segment_index)

## The nearest point on a river to p, as [distance, point, flow direction]; distance is huge when there is none in reach.
func _river_near(p: Vector2) -> Array:
	var best_squared := 1e18
	var best_point := p
	var best_dir := Vector2.ZERO
	var key := Vector2i(int(floor(p.x / RIVER_GRID_SIZE)), int(floor(p.y / RIVER_GRID_SIZE)))
	for segment_index in river_grid.get(key, []):
		var s: Array = river_segs[segment_index]
		var a: Vector2 = s[0]
		var ab: Vector2 = s[1] - a
		var length_squared := ab.length_squared()
		var along := clampf((p - a).dot(ab) / length_squared, 0.0, 1.0) if length_squared > 0.000001 else 0.0
		var at := a + ab * along
		var d := (p - at).length_squared()
		if d < best_squared:
			best_squared = d
			best_point = at
			best_dir = ab.normalized() if length_squared > 0.000001 else Vector2.ZERO
	return [sqrt(best_squared), best_point, best_dir]

const BOAT_REACH := 0.6      # a fisher this close to water takes to a boat; closer still and the boat is fully under them
const BOAT_NEAR := 0.1

## Where a fisher at p would float: the nearest river point or just off the nearest shore, with the boat's heading and how
## fully it has taken them (0 out of reach .. 1 afloat). Returns [point, heading, strength].
func _boat_spot(p: Vector2) -> Array:
	var best := BOAT_REACH
	var point := p
	var heading := Vector2.ZERO
	var river := _river_near(p)
	if float(river[0]) < best:
		best = river[0]
		point = river[1]
		heading = river[2]
	var t: int = w.tile_at(p)
	if t >= 0:
		var c: Vector2 = w.tile_center(t)
		for i in 6:
			var nb: int = w.neighbour(t, i)
			if nb >= 0 and w.is_water(nb):
				var wc: Vector2 = w.tile_center(nb)
				var q := c.lerp(wc, 0.62)
				var d := p.distance_to(q)
				if d < best:
					best = d
					point = q
					heading = (wc - c).normalized().rotated(PI * 0.5)
	return [point, heading, 1.0 - smoothstep(BOAT_NEAR, BOAT_REACH, best)]

func _river_dist(p: Vector2) -> float:
	var best_squared := 1e18
	var key := Vector2i(int(floor(p.x / RIVER_GRID_SIZE)), int(floor(p.y / RIVER_GRID_SIZE)))
	for segment_index in river_grid.get(key, []):
		var s: Array = river_segs[segment_index]
		var a: Vector2 = s[0]
		var ab: Vector2 = s[1] - a
		var length_squared := ab.length_squared()
		var along := clampf((p - a).dot(ab) / length_squared, 0.0, 1.0) if length_squared > 0.000001 else 0.0
		var offset := p - (a + ab * along)
		best_squared = minf(best_squared, offset.length_squared())
	return sqrt(best_squared)

# ------------------------------------------------------------------ vegetation

func _rand_in_hex(t: int, k: int, r: float) -> Vector2:
	var xy := w.tile_xy(t)
	var c := w.tile_center(t)
	for attempt in 6:
		var u := M7WorldData.hash01(xy.x * 31 + k, xy.y * 17 + attempt, 101) * 2.0 - 1.0
		var v := M7WorldData.hash01(xy.x * 13 + k, xy.y * 29 + attempt, 202) * 2.0 - 1.0
		var p := Vector2(u * 0.5, v * M7WorldData.R) * r
		# inside pointy-top hex of circumradius R*r?
		var ax := absf(p.x)
		var az := absf(p.y)
		if ax <= 0.5 * r and az <= M7WorldData.R * r - ax * 0.57735:
			return c + p
	return c

func _vegetation() -> void:
	tree_points.clear()
	var B := M7WorldData.Biome
	var count := 0
	for t in w.width * w.height:
		var b := int(w.biome[t])
		if b <= B.COAST:
			continue
		if t < w.visibility.size() and w.visibility[t] == 0:
			continue
		var res := int(w.reserved[t])
		if res >= 2:
			# settlement tile or farmed plot: only a few tufts at the rim
			continue
		var h: float = w.heights[t]
		var xy := w.tile_xy(t)
		var near := res == 1
		var river := (w.flags[t] & M7WorldData.FLAG_RIVER) != 0
		var trees := 0
		var tree_kind := ""
		var bushes := 0
		var tufts := 0
		var rocks := 0
		var reeds := 0
		var tuft_kind := "tuft0"
		var hk := M7WorldData.hash01(xy.x, xy.y, 55)
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
				var s := 0.75 + 0.45 * M7WorldData.hash01(xy.x, xy.y, 57)
				var sy := 0.6 + 0.8 * M7WorldData.hash01(xy.x, xy.y, 58)
				var off := Vector2(M7WorldData.hash01(xy.x, xy.y, 59) - 0.5, M7WorldData.hash01(xy.x, xy.y, 60) - 0.5) * 0.36
				var mc := w.tile_center(t) + off
				var xf := Transform3D(rot_y(hk * TAU).scaled(Vector3(s, s * sy, s)), Vector3(mc.x, h - 0.03, mc.y))
				props.add("mountain%d" % mi, xf, Color(1, 1, 1, hk))
				if M7WorldData.hash01(xy.x, xy.y, 61) > 0.6:
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
		if w.flags[t] & M7WorldData.FLAG_COAST_ADJ:
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
			var hv := M7WorldData.hash01(xy.x * 7 + i, xy.y, 61)
			match tree_kind:
				"conifer": name = "conifer%d" % int(hv * 3.0)
				"broadleaf": name = "broadleaf%d" % int(hv * 3.0)
				"acacia": name = "acacia%d" % int(hv * 2.0)
				"jungle": name = ("jungle%d" % int(hv * 2.0)) if hv < 0.75 else "broadleaf%d" % int(hv * 3.0 - 2.0)
				"mixed": name = ("conifer%d" % int(hv * 3.0)) if M7WorldData.hash01(xy.x, xy.y * 3 + i, 62) < 0.45 else "broadleaf%d" % int(hv * 3.0)
			var s := 0.8 + 0.45 * M7WorldData.hash01(xy.x, xy.y + i, 63)
			var y := w.height_at(p)
			props.add(name, _xf(p, y, hv * TAU, s), _tint(M7WorldData.hash01(xy.x + i, xy.y, 64)))
			tree_points.append(p)
			count += 1
		for i in bushes:
			var p := _rand_in_hex(t, k, 0.9)
			k += 1
			if river and _river_dist(p) < 0.1:
				continue
			var hv := M7WorldData.hash01(xy.x * 5 + i, xy.y, 71)
			var nm := "shrub" if (b == B.TUNDRA or b == B.DESERT or b == B.SAVANNA) and hv > 0.4 else "bush"
			props.add(nm, _xf(p, w.height_at(p), hv * TAU, 0.8 + 0.5 * hv), _tint(hv))
		for i in tufts:
			var p := _rand_in_hex(t, k, 0.95)
			k += 1
			var hv := M7WorldData.hash01(xy.x * 3 + i, xy.y, 81)
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
			var hv := M7WorldData.hash01(xy.x * 17 + i, xy.y, 83)
			var cl := _grove(int(p.x * 3.0), int(p.y * 3.0))
			if hv > 0.35 + 0.6 * cl:
				continue
			props.add(dname, _xf(p, w.height_at(p), hv * TAU, 0.6 + 0.7 * hv), _tint(hv))
		for i in rocks:
			var p := _rand_in_hex(t, k, 0.8)
			k += 1
			var hv := M7WorldData.hash01(xy.x * 9 + i, xy.y, 91)
			props.add("rock%d" % int(hv * 3.0), _xf(p, w.height_at(p) - 0.01, hv * TAU, 0.7 + 0.8 * hv), Color(1, 1, 1, hv))
		for i in reeds:
			var p: Vector2
			if river:
				# along the river banks
				p = _rand_in_hex(t, k, 0.6)
			else:
				p = _shore_point(t, k)
			k += 1
			var hv := M7WorldData.hash01(xy.x * 11 + i, xy.y, 95)
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
	var a := M7WorldData.hash01(ix, iy, 777)
	var b := M7WorldData.hash01(ix + 1, iy, 777)
	var c := M7WorldData.hash01(ix, iy + 1, 777)
	var d := M7WorldData.hash01(ix + 1, iy + 1, 777)
	return lerpf(lerpf(a, b, ux), lerpf(c, d, ux), uy)

## A point near the edge of t that faces water.
func _shore_point(t: int, k: int) -> Vector2:
	var c := w.tile_center(t)
	for i in 6:
		var e := (i + k) % 6
		var nb := w.neighbour(t, e)
		if nb >= 0 and w.is_water(nb):
			var a: Vector2 = M7WorldData.corner(e)
			var b: Vector2 = M7WorldData.corner((e + 1) % 6)
			var f := M7WorldData.hash01(t, k, 97)
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
	if w.biome[t] >= M7WorldData.Biome.MOUNTAIN:
		return false
	if _river_dist(p) < 0.11:
		return false
	return true

func _settlements() -> void:
	var K := M7WorldData.Kind
	hearths.clear()
	villages.clear()
	# fields and orchards on their plot tiles
	for pi in range(0, w.plots.size(), 2):
		var t := w.plots[pi]
		if not w.is_land(t):
			continue
		var c := w.tile_center(t)
		var xy := w.tile_xy(t)
		var hv := M7WorldData.hash01(xy.x, xy.y, 301)
		var h: float = w.heights[t]
		if w.plots[pi + 1] == 0:
			props.add("field", _xf(c, h, PI / 3.0 * int(hv * 3.0) + PI / 6.0 * 0.0, 1.0), Color(1, 1, 1, hv))
		else:
			for i in 7:
				var p := c if i == 6 else c + Vector2(cos(TAU * i / 6.0), sin(TAU * i / 6.0)) * 0.27
				var hv2 := M7WorldData.hash01(xy.x + i, xy.y, 303)
				props.add("orchard_tree", _xf(p, h, hv2 * TAU, 0.9 + 0.3 * hv2), _tint(hv2))
			for i in 6:
				var p := _rand_in_hex(t, 40 + i, 0.9)
				props.add("tuft0", _xf(p, h, i * 1.3, 1.0), _tint(0.5))

	for si in w.settlements.size():
		var s: Dictionary = w.settlements[si]
		var t := int(s.tile)
		var civ := int(s.civ)
		var st: Array = s.structures
		var cc := M7MeshBuilder.lin(M7WorldData.civ_color(civ), 0.5)
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
		if bool(s.get("remembered")):
			villages.append(Vector3(c.x, c.y, 0.25))
			continue
		if st[K.HEARTH] > 0:
			props.add("hearth", _xf(c, h0, seed * TAU, VS), Color(1, 1, 1, seed))
			props.add("flame", _xf(c, h0 + 0.004, seed * TAU, VS), Color(1, 1, 1, seed))
		props.add("banner%d" % civ, _xf(c + Vector2(0.09, -0.07), h0, -0.4, VS), Color(cc.r, cc.g, cc.b, seed))

		# items to lay out on a sunflower spiral around the hearth
		var items := _dwellings(st, int(s.get("halls", 0)), int(s.population))
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
		var placed_r := []
		var slot := 0
		var rmax := 0.0
		for it in items:
			var p := Vector2.ZERO
			var found := false
			var ri := _item_radius(it)
			while slot < 160:
				var a := slot * 2.39996 + seed * TAU
				var r := 0.112 * VS * sqrt(slot + 3.2)
				slot += 1
				var q := c + Vector2(cos(a), sin(a)) * r
				if not _ok_site(q, h0):
					continue
				var clear := true
				for k in placed.size():
					if (placed[k] as Vector2).distance_to(q) < (placed_r[k] + ri) * VS:
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
			placed_r.append(ri)
			rmax = maxf(rmax, p.distance_to(c))
			var d := c - p
			var ang := atan2(d.x, d.y)
			var sc := VS * (0.95 + 0.15 * M7WorldData.hash01(t, placed.size(), 5))
			props.add(it, _xf(p, w.height_at(p), ang, sc), Color(cc.r, cc.g, cc.b, M7WorldData.hash01(t, placed.size(), 6)))
		villages.append(Vector3(c.x, c.y, rmax * 0.75 + 0.08))
		var sea: int = w.sea_tier[civ] if civ >= 0 and civ < w.sea_tier.size() else 0
		if sea > 0:
			_moor_fleet(t, c0, seed, sea, int(s.population))
		var decor = s.get("decor", PackedInt32Array())
		if decor.size() > 0:
			_place_decor(decor, t, c, c0, h0, seed, rmax, placed, cc, st[K.THORN_BARRIER] > 0)
		# lookout tower with a big banner at the edge
		if st[K.LOOKOUT] > 0:
			for i in 12:
				var a := TAU * i / 12.0 + seed * 3.0
				var q := c + Vector2(cos(a), sin(a)) * (rmax + 0.12)
				if _ok_site(q, h0):
					props.add("lookout", _xf(q, w.height_at(q), a, VS), Color(cc.r, cc.g, cc.b, seed))
					props.add("banner_small%d" % civ, _xf(q, w.height_at(q) + 0.285 * VS, -0.4, VS), Color(cc.r, cc.g, cc.b, seed))
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
				var q := c + Vector2(cos(a), sin(a)) * ring * (1.0 + 0.08 * sin(a * 3.0 + seed * 7.0) + 0.04 * (M7WorldData.hash01(t, i, 7) - 0.5))
				if M7WorldData.hash01(t, i, 10) < 0.04:
					continue
				var tq := w.tile_at(q)
				if tq < 0 or not w.is_land(tq) or _river_dist(q) < 0.08:
					continue
				var tang := a + PI * 0.5
				props.add("thorn", _xf(q, w.height_at(q), -tang, 1.2 + 0.1 * M7WorldData.hash01(t, i, 8)), Color(1, 1, 1, M7WorldData.hash01(t, i, 9)))
		# weir in the nearest water
		if st[K.WEIR] > 0:
			for i in 6:
				var nb := w.neighbour(t, i)
				if nb >= 0 and w.is_water(nb):
					var q := (c0 + w.tile_center(nb)) * 0.5 + (w.tile_center(nb) - c0) * 0.12
					props.add("weir", _xf(q, -0.02, atan2((w.tile_center(nb) - c0).x, (w.tile_center(nb) - c0).y), 1.0), Color(1, 1, 1, seed))
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
				props.add("weir", _xf(bq, w.height_at(bq) + 0.02, atan2(bd.x, bd.y), 0.55), Color(1, 1, 1, seed))
		# pasture: a fenced paddock with a few sheep on a free neighbour tile
		if st[K.PASTURE] > 0:
			for i in 6:
				var nb := w.neighbour(t, (i + int(seed * 6.0)) % 6)
				if nb < 0 or not w.is_land(nb) or w.plot_kind[nb] >= 0 or w.biome[nb] >= M7WorldData.Biome.MOUNTAIN:
					continue
				if w.flags[nb] & M7WorldData.FLAG_RIVER:
					continue
				var pc := w.tile_center(nb)
				var ph: float = w.heights[nb]
				var pr := 0.3
				for e in 6:
					var a0 := M7WorldData.corner(e) / M7WorldData.R * pr
					var a1 := M7WorldData.corner((e + 1) % 6) / M7WorldData.R * pr
					for half in 2:
						var q := pc + a0.lerp(a1, 0.25 + 0.5 * half)
						var dd := a1 - a0
						props.add("fence", _xf(q, ph, -atan2(dd.y, dd.x), 1.15), Color(1, 1, 1, 0.5))
				for j in 4:
					var q := pc + Vector2(cos(j * 1.7 + seed), sin(j * 1.7 + seed)) * (0.06 + 0.04 * j)
					var col := M7MeshBuilder.lin(Color("#e8dfcc") if j != 2 else Color("#6e5a48"), 0.5)
					props.add("animal_woolly", _xf(q, ph, j * 2.1 + seed, 1.0), _glide_custom((6 * 8 + 4) if j != 2 else (9 * 8 + 3), 0, 0.5, 0.0, Vector2.ZERO))
				break

## Dwellings a village shows: what it has built, newest first, not an even mix of everything it ever knew. Outgrown types
## (lean-tos and tents once there are permanent houses, and so on up the ladder) are drawn no more; the people the built houses
## do not cover live in the best small house the village knows (or in more longhouses when that is all it has).
const DWELL_CAPACITY := {"leanto": 4, "tent": 5, "pithouse": 6, "wattle": 8, "mudbrick": 8, "longhouse": 25, "hall": 30}
const DWELL_RADIUS := {"leanto": 0.085, "tent": 0.08, "pithouse": 0.105, "wattle": 0.1, "mudbrick": 0.095, "longhouse": 0.165, "hall": 0.2}
const DWELL_MAX_EACH := 10
const DWELL_MAX_TOTAL := 36

func _item_radius(mesh_name: String) -> float:
	return DWELL_RADIUS.get(mesh_name, 0.075)

func _dwellings(st: Array, halls: int, pop: int) -> Array:
	var K := M7WorldData.Kind
	var built := {
		"leanto": int(st[K.LEAN_TO]), "tent": int(st[K.HIDE_TENT]), "pithouse": int(st[K.PIT_HOUSE]),
		"wattle": int(st[K.WATTLE_HOUSE]), "mudbrick": int(st[K.MUDBRICK_HOUSE]), "longhouse": int(st[K.LONGHOUSE]),
		"hall": halls}
	var filler := "leanto"
	for nm in ["leanto", "tent", "pithouse", "wattle", "mudbrick"]:
		if built[nm] > 0:
			filler = nm
	var shown := {}
	for nm in built:
		shown[nm] = mini(int(built[nm]), DWELL_MAX_EACH)
	if built.pithouse + built.wattle + built.mudbrick + built.longhouse > 0:
		shown.leanto = 0
		shown.tent = 0
	if built.wattle + built.mudbrick + built.longhouse > 0:
		shown.pithouse = 0
	if built.mudbrick + built.longhouse > 0:
		shown.wattle = 0
	var covered := 0
	for nm in shown:
		covered += int(shown[nm]) * int(DWELL_CAPACITY[nm])
	var short := maxi(0, pop - covered)
	var out := []
	for nm in ["hall", "longhouse", "mudbrick", "wattle", "pithouse", "tent", "leanto"]:
		for i in int(shown[nm]):
			out.append(nm)
	if short > 0:
		if filler == "leanto" and built.longhouse > 0 and built.pithouse + built.wattle + built.mudbrick == 0:
			filler = "longhouse"
		var more := ceili(float(short) / float(DWELL_CAPACITY[filler]))
		for i in more:
			if out.size() >= DWELL_MAX_TOTAL:
				break
			out.append(filler)
	if out.is_empty() and pop > 0:
		out.append("leanto")
	return out

## Kinds that sit on the village edge rather than among the houses.
const DECOR_FAR := ["cairn", "shrine", "signal", "charcoal", "grave", "quarry", "skypole"]

## Props a village earns from what its people know (see DecorTable.cs). Placed on an outward spiral clear of the houses; the
## palisade rings the village, `placed` holds the house spots so nothing overlaps.
func _place_decor(decor, t: int, c: Vector2, c0: Vector2, h0: float, seed: float, rmax: float, placed: Array, cc: Color,
		has_thorns: bool) -> void:
	var near_r := rmax + 0.17
	var far_r := rmax + 0.3
	var slot := 0
	for kind in decor:
		var kind_i := int(kind)
		if kind_i < 0 or kind_i >= M7MeshKit.DECOR_NAMES.size():
			continue
		var decor_name := String(M7MeshKit.DECOR_NAMES[kind_i])
		var mesh_name := "decor_" + decor_name
		var tint := Color(cc.r, cc.g, cc.b, M7WorldData.hash01(t, kind_i, 11))
		if decor_name == "palisade":
			_decor_palisade(t, c, seed, rmax + (0.2 if has_thorns else 0.13), tint)
			continue
		var base := far_r if DECOR_FAR.has(decor_name) else near_r
		var found := false
		var p := Vector2.ZERO
		for k in 40:
			var a := seed * TAU + (slot + k) * 2.39996
			var q := c + Vector2(cos(a), sin(a)) * (base + 0.035 * (k / 8))
			if not _ok_site(q, h0):
				continue
			var clear := true
			for pp in placed:
				if (pp as Vector2).distance_to(q) < 0.17:
					clear = false
					break
			if clear:
				p = q
				found = true
				break
		slot += 3
		if not found:
			continue
		placed.append(p)
		var d := c - p
		var ang := atan2(d.x, d.y)
		var sc := VS * (0.95 + 0.12 * M7WorldData.hash01(t, kind_i, 12))
		var y := w.height_at(p)
		props.add(mesh_name, _xf(p, y, ang, sc), tint)
		if decor_name == "signal":
			props.add("flame", _xf(p, y + 0.05 * sc, ang, 0.8), Color(1, 1, 1, seed))

## A ring of palisade segments with a gate, outside the houses (and the thorn hedge if there is one).
func _decor_palisade(t: int, c: Vector2, seed: float, ring: float, tint: Color) -> void:
	var nseg := int(ceil(TAU * ring / 0.13))
	var gate := int(seed * nseg)
	for i in nseg:
		if i == gate or i == (gate + 1) % nseg:
			continue
		var a := TAU * (i + 0.5) / nseg
		var q := c + Vector2(cos(a), sin(a)) * ring * (1.0 + 0.05 * sin(a * 3.0 + seed * 7.0))
		var tq := w.tile_at(q)
		if tq < 0 or not w.is_land(tq) or _river_dist(q) < 0.08:
			continue
		var tang := a + PI * 0.5
		props.add("decor_palisade", _xf(q, w.height_at(q), -tang, 1.1), tint)

## The boats a village keeps: one to four (by population) moored along the nearest shore or river bank, of the best kind its
## civilization knows (1 raft .. 4 outrigger). Nothing happens if there is no water within a short walk.
func _moor_fleet(t: int, c0: Vector2, seed: float, tier: int, population: int) -> void:
	var count := clampi(population / 8, 1, 4)
	var mesh_name := "boat_%d" % tier
	var anchor := Vector2.ZERO
	var along := Vector2.ZERO
	var level := 0.0
	var found := false
	for i in 6:
		var nb: int = w.neighbour(t, (i + int(seed * 6.0)) % 6)
		if nb >= 0 and w.is_water(nb):
			var dir: Vector2 = w.tile_center(nb) - c0
			anchor = c0 + dir * 0.6
			along = dir.normalized().rotated(PI * 0.5)
			level = 0.004
			found = true
			break
	if not found:
		# the river bank about a short walk from the hearth, not under the houses
		var best := 1e9
		for sgi in river_segs:
			var a2: Vector2 = sgi[0]
			var dist := a2.distance_to(c0)
			if dist > 1.2:
				continue
			var gap := absf(dist - 0.7)
			if gap < best:
				best = gap
				anchor = a2
				along = ((sgi[1] as Vector2) - a2).normalized()
				level = -1.0
				found = true
	if not found or along == Vector2.ZERO:
		return
	for k in count:
		var off := (k - (count - 1) * 0.5) * 0.2
		var q := anchor + along * off
		var tq := w.tile_at(q)
		if tq < 0:
			continue
		var y := (maxf(w.height_at(q), 0.0) + 0.012) if level < 0.0 else level
		var hv := M7WorldData.hash01(t, k, 31)
		var yaw := atan2(along.x, along.y) + (hv - 0.5) * 0.35
		props.add(mesh_name, _xf(q, y, yaw, 1.0), Color(1, 1, 1, hv))

func _camps() -> void:
	for cp in w.camps:
		var t := int(cp[0])
		var civ := int(cp[1])
		if not w.is_land(t):
			continue
		var c := w.tile_center(t) + Vector2(0.12, 0.05)
		var h: float = w.heights[t]
		var cc := M7MeshBuilder.lin(M7WorldData.civ_color(civ), 0.5)
		var seed := float(t % 113) / 113.0
		villages.append(Vector3(c.x + 0.05, c.y + 0.05, 0.12))
		props.add("camp", _xf(c, h, seed * TAU, 1.0), Color(cc.r, cc.g, cc.b, seed))
		props.add("camp", _xf(c + Vector2(0.1, 0.06), h, seed * TAU + 2.0, 0.85), Color(cc.r, cc.g, cc.b, seed))
		props.add("hearth", _xf(c + Vector2(0.03, 0.12), h, seed, 0.7), Color(1, 1, 1, seed))
		props.add("flame", _xf(c + Vector2(0.03, 0.12), h + 0.003, seed, 0.6), Color(1, 1, 1, seed))
		props.add("banner_small%d" % civ, _xf(c + Vector2(-0.07, -0.02), h, -0.4, 1.0), Color(cc.r, cc.g, cc.b, seed))

# ------------------------------------------------------------------ people

## `--boat-debug` puts every adult near water in a boat, tier 1 to 4 by figure index.
var BOAT_DEBUG: bool = "--boat-debug" in OS.get_cmdline_user_args()

func _debug_boat_tier(fi: int) -> int:
	return 1 + (fi / 11) % 4

## `--tier-debug` gives every adult a tool and cycles the tier by figure index so all four looks can be seen at once.
var TIER_DEBUG: bool = "--tier-debug" in OS.get_cmdline_user_args()

const TOOL_BY_ACTIVITY := {
	0: "plain", 1: "basket", 2: "spear", 3: "pole", 4: "hoe", 5: "axe", 6: "basket", 7: "hammer", 8: "hammer",
	9: "plain", 10: "plain", 11: "plain", 12: "plain", 15: "bundle", 16: "bundle", 17: "spear", 18: "spear", 19: "spear",
}

## Where a figure stands on a given map position: people at home gather around the hearth, away from the river.
## Returns [world position, facing].
func _place_figure(mx: int, my: int, home: int, face: float) -> Array:
	var p := w.map_to_world2(float(mx), float(my))
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
			face = atan2(to.x, to.y) + (face / TAU - 0.5) * 1.2
	return [p, face]

## Per-instance custom data for gliding actors (see prop.gdshader):
## r = palette index + mode * stride + seed fraction, g = height offset, b/a = ground offset from where it stands now to
## where it was one publish ago. The shader eases that offset away with the global fig_lerp.
static func _glide_custom(palette: int, mode_offset: int, seed: float, dy: float, offset: Vector2) -> Color:
	return Color(float(palette + mode_offset) + seed * 0.98, dy, offset.x, offset.y)

func _figures() -> void:
	var n := 0
	for fi in range(0, w.figures.size(), 11):
		var mx := w.figures[fi]
		var my := w.figures[fi + 1]
		var civ := w.figures[fi + 2]
		var age := w.figures[fi + 3]
		var act := w.figures[fi + 4]
		var fl := w.figures[fi + 5]
		var rung := w.figures[fi + 6]
		var home := w.figures[fi + 7]
		var id := w.figures[fi + 8]
		var pmx := w.figures[fi + 9]
		var pmy := w.figures[fi + 10]
		var seed := M7WorldData.hash01(id & 0xffff, (id >> 16) & 0x3fff, 7)
		var tint := 8 if (fl & 1024) else civ % 8
		var mode_offset := 16 if pmx < 0 else 0     # new this publish: grow in
		var placed := _place_figure(mx, my, home, seed * TAU)
		var p: Vector2 = placed[0]
		var face: float = placed[1]
		# fishers who know a boat take to it near a river or shore (not while still at home)
		var boat_tier := 0
		var boat_strength := 0.0
		var boat_heading_set := false
		if (act == 3 or BOAT_DEBUG) and age == 1 and not regional_lod and civ >= 0 and civ < w.sea_tier.size():
			boat_tier = _debug_boat_tier(fi) if BOAT_DEBUG else w.sea_tier[civ]
			if boat_tier > 0 and (BOAT_DEBUG or (home >= 0 and home < w.settlements.size() and w.tile_at(p) != int(w.settlements[home].tile))):
				var spot := _boat_spot(p)
				boat_strength = spot[2]
				if boat_strength > 0.0:
					p = p.lerp(spot[0], boat_strength)
					if (spot[1] as Vector2) != Vector2.ZERO:
						face = atan2((spot[1] as Vector2).x, (spot[1] as Vector2).y)
						boat_heading_set = true
		# anyone standing on open water (a party under way to an island) is in a boat of their civilization's best kind
		if boat_strength < 1.0 and not regional_lod and civ >= 0 and civ < w.sea_tier.size() and w.sea_tier[civ] > 0:
			var here: int = w.tile_at(p)
			if here >= 0 and w.is_water(here):
				boat_strength = 1.0
				boat_tier = w.sea_tier[civ]
		var y := maxf(w.height_at(p), 0.0)
		if boat_strength > 0.0:
			y += 0.006 * boat_strength
		var offset := Vector2.ZERO
		var dy := 0.0
		if pmx >= 0 and (pmx != mx or pmy != my):
			var pp: Vector2 = _place_figure(pmx, pmy, home, seed * TAU)[0]
			if boat_strength > 0.0:
				var prev_spot := _boat_spot(pp)
				pp = pp.lerp(prev_spot[0], prev_spot[2])
			offset = pp - p
			dy = maxf(w.height_at(pp), 0.0) - y
			if offset.length() > 0.004 and not boat_heading_set:
				face = atan2(-offset.x, -offset.y)     # walking from pp to p
		var custom := _glide_custom(tint, mode_offset, seed, dy, offset)
		if regional_lod:
			props.add("person_far", _xf(p, y, 0.0, 1.0), custom)
			n += 1
			continue
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
			# bladed and headed tools show the tier of the method in use: stone, polished stone, copper, bronze
			var tier := M7MeshKit.tool_tier_for_rung(rung) if rung >= 0 else 0
			if TIER_DEBUG:
				var idx := fi / 11
				mesh = ["person_spear", "person_axe", "person_hoe", "person_hammer"][idx % 4]
				tier = (idx / 3) % M7MeshKit.TOOL_TIERS
			if tier > 0 and mesh in ["person_spear", "person_axe", "person_hoe", "person_hammer"]:
				mesh += "_t" + str(tier)
		if boat_strength > 0.0:
			# the fisher stands in the boat; the hull shrinks away as they leave the water
			props.add("person_boat_%d" % boat_tier, _xf(p, y - 0.006 * boat_strength, face, boat_strength), custom)
			y += 0.014 * boat_strength
			boat_probe = p
		props.add(mesh, _xf(p, y, face, 1.0), custom)
		var carry := (fl >> 16) & 15
		if TIER_DEBUG and carry == 0:
			carry = 1 + (fi / 11) % 8
		if carry > 0 and carry < M7MeshKit.CARRY_NAMES.size() and age == 1 and act != 13 and act != 14:
			props.add("person_carry_" + M7MeshKit.CARRY_NAMES[carry], _xf(p, y, face, 1.0), custom)
		props.add("shadow_disc", _xf(p, y + 0.001, 0.0, 1.0), Color(offset.x, dy, offset.y, float(mode_offset / 16)))
		n += 1
	# people who just left: stay where they stood for one glide and fade out
	if not regional_lod:
		for gi in range(0, w.ghosts.size(), 8):
			var gmx := w.ghosts[gi]
			var gmy := w.ghosts[gi + 1]
			var gcivv := w.ghosts[gi + 2]
			var gfl := w.ghosts[gi + 5]
			var ghome := w.ghosts[gi + 6]
			var gid := w.ghosts[gi + 7]
			var gseed := M7WorldData.hash01(gid & 0xffff, (gid >> 16) & 0x3fff, 7)
			var gp: Vector2 = _place_figure(gmx, gmy, ghome, gseed * TAU)[0]
			var gy := maxf(w.height_at(gp), 0.0)
			var gtint := 8 if (gfl & 1024) else gcivv % 8
			props.add("person_plain", _xf(gp, gy, gseed * TAU, 1.0), _glide_custom(gtint, 32, gseed, 0.0, Vector2.ZERO))
			props.add("shadow_disc", _xf(gp, gy + 0.001, 0.0, 1.0), Color(0.0, 0.0, 0.0, 2.0))
	stats.figures = n
	if "--perf-debug" in OS.get_cmdline_user_args():
		var moved := 0
		var added := 0
		var maxd := 0.0
		for fi in range(0, w.figures.size(), 11):
			if w.figures[fi + 9] < 0:
				added += 1
			elif w.figures[fi + 9] != w.figures[fi] or w.figures[fi + 10] != w.figures[fi + 1]:
				moved += 1
				maxd = maxf(maxd, Vector2(w.figures[fi + 9] - w.figures[fi], w.figures[fi + 10] - w.figures[fi + 1]).length() / w.MAP_UNITS)
		print("GLIDE figures=", n, " moved=", moved, " new=", added, " ghosts=", w.ghosts.size() / 8, " max_tiles=", snappedf(maxd, 0.01))

func _herds() -> void:
	var n := 0
	for hi in range(0, w.herds.size(), 7):
		var mx := w.herds[hi]
		var my := w.herds[hi + 1]
		var sp := clampi(w.herds[hi + 2], 0, SPECIES.size() - 1)
		var animals := w.herds[hi + 3]
		var id := w.herds[hi + 4]
		var pmx := w.herds[hi + 5]
		var pmy := w.herds[hi + 6]
		var p := w.map_to_world2(float(mx), float(my))
		var pp := p
		var moved := pmx >= 0 and (pmx != mx or pmy != my)
		if moved:
			pp = w.map_to_world2(float(pmx), float(pmy))
		var cfg: Array = SPECIES[sp]
		var cnt := clampi(int(round(animals / 7.0)), 2, 7)
		var heading := M7WorldData.hash01(id, 0, 1) * TAU
		if moved and (p - pp).length() > 0.004:
			heading = atan2(p.x - pp.x, p.y - pp.y)
		var mode_offset := 128 if pmx < 0 else 0
		for i in cnt:
			var a := i * 2.39996 + M7WorldData.hash01(id, 0, 1) * TAU
			var r := 0.055 * sqrt(i + 0.5) * float(cfg[2])
			var q := p + Vector2(cos(a), sin(a)) * r
			var t := w.tile_at(q)
			if t < 0 or not w.is_land(t):
				continue
			var hv := M7WorldData.hash01(id + i, 0, 2)
			var offset := pp - p
			var dy := 0.0
			if moved:
				dy = maxf(w.height_at(q + offset), 0.0) - w.heights[t]
			else:
				offset = Vector2.ZERO
			var bin := clampi(int(hv * 8.0), 0, 7)
			props.add(cfg[0], _xf(q, w.heights[t], heading + (hv - 0.5) * 1.4, float(cfg[2]) * (0.85 + 0.25 * hv)),
				_glide_custom(sp * 8 + bin, mode_offset, hv, dy, offset))
			n += 1
	stats.animals = n

func _beacons() -> void:
	for beacon in w.beacons:
		var t := int(beacon[0])
		if not w.is_land(t) or (t < w.visibility.size() and w.visibility[t] == 0):
			continue
		var c := w.tile_center(t)
		var cc := M7WorldData.civ_color(int(beacon[1]))
		props.add("beacon", _xf(c, w.heights[t], float(t) * 0.03, 1.25), Color(cc.r, cc.g, cc.b, 0.5))
