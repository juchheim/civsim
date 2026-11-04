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
var settlement_multimeshes := {}
var actor_multimeshes := {}
var herd_props: Node3D
var herd_multimeshes := {}
var deposit_props: Node3D
var deposit_multimeshes := {}
var deposit_signature := -1
var herds_dirty := false   # herds and beacons rebuild one frame after people, so a publish never costs two frames' worth
var border_node: MeshInstance3D
var borders_far_node: MeshInstance3D
var river_node: MeshInstance3D
var structure_signature := -1
var rig: M7MonumentRig
var boat_probe := Vector2.ZERO          # the last boat placed (dev scenarios aim the camera at it)
var chariot_probe := Vector2.ZERO       # the last chariot placed (dev scenarios aim the camera at it)
var decor_spots := {}                   # decor name -> where the last one was placed (dev scenarios aim the camera at it)
var trail_node: MeshInstance3D
var trail_signature := -1
var trail_polys: Array = []              # centre lines of the trail ribbons (trees are cleared along them)
var trails_changed := false
var road_node: MeshInstance3D
var road_signature := -1
var border_signature := -1
var river_visibility_signature := -1
var regional_lod := false
var figure_tints := PackedColorArray()
var freefolk_tint: Color
var hearths := []          # world positions of settlement hearths (index = settlement)
var villages := []         # Vector3(x, z, radius) of trampled ground
var civic_sites: Array = []               # {kind, p, y, yaw, civ, seed, tile}: where each civic building (temple ...) stands, for its rig
var beacon_fires: Array = []            # {top, seed}: the Great Lighthouse's fire, for the war rig
var lighthouse_sites: Array = []        # {p, y}: where each Great Lighthouse stands
var aqueduct_sites: Array = []          # {p, y}: the aqueducts, for the scenarios' cameras
var frontier_sites: Array = []          # {p, y}: the frontier wall runs
var forge_sites: Array = []             # {chimney, mouth, seed}: forges at work, for the war rig's flame and smoke
var siege_engines: Array = []           # {p, yaw, target, seed, k}: each siege engine standing round a besieged town, for the war rig
var harbor_sites: Array = []            # {p, y}: where each harbor stands
var bridge_sites: Array = []            # {p, y}: where each stone bridge stands
var castle_sites: Array = []            # {p, y, yaw, stage, civ, tile}: where each castle (DEF-08) stands or is going up
var ziggurat_sites: Array = []           # {p, y, yaw, stage, civ, seed, tile}: where each ziggurat (BLD-08) stands, for its rig
var tree_points := PackedVector2Array()   # where full-size trees stand (the megalith ring keeps clear of them)
var river_segs := []       # [Vector2 a, Vector2 b] for layout avoidance
var river_grid := {}       # 4-unit cells to nearby river segment indices
var stats := {}
const ArtDefense := preload("res://Renderer/LowPoly3D/Scripts/art_defense.gd")
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
	road_node = MeshInstance3D.new()
	road_node.name = "Roads"
	road_node.material_override = M7TrailBuilder.make_material()
	road_node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	root.add_child(road_node)
	_update_trails()
	_update_roads()
	props = M7Batcher.new()
	_figures()
	actor_props = _build_props_node("Actors", props)
	props = M7Batcher.new()
	_herds()
	_beacons()
	herd_props = _build_props_node("Herds", props)
	props = M7Batcher.new()
	deposit_signature = _deposit_digest()
	_deposits()
	deposit_props = _build_props_node("Deposits", props)
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
	_update_roads()
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
		settlement_multimeshes = props.update_existing(settlement_props, kit, _material_for, settlement_multimeshes)
		props = M7Batcher.new()
	rig.set_monuments(w.monuments, structure_signature)
	var new_deposit_signature := _deposit_digest()
	if new_deposit_signature != deposit_signature:
		deposit_signature = new_deposit_signature
		props = M7Batcher.new()
		_deposits()
		deposit_multimeshes = props.update_existing(deposit_props, kit, _material_for, deposit_multimeshes)
		props = M7Batcher.new()
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
	elif node_name == "SettlementProps":
		settlement_multimeshes = batcher.update_existing(node, kit, _material_for, {})
		count = batcher.instance_count
	elif node_name == "Herds":
		herd_multimeshes = batcher.update_existing(node, kit, _material_for, {})
		count = batcher.instance_count
	elif node_name == "Deposits":
		deposit_multimeshes = batcher.update_existing(node, kit, _material_for, {})
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

## Rebuilds the paved roads when a road is finished, gains its bridge, or is seen (TRN-07).
func _update_roads() -> void:
	var sig := hash(w.road_meta) * 31 + hash(w.road_tiles)
	if sig == road_signature:
		return
	road_signature = sig
	road_node.mesh = M7TrailBuilder.build_roads(w, trail_polys)

## Rebuilds the trail ribbons when the routes, their wear or what is explored along them changes.
func _update_trails() -> void:
	var sig := hash(w.trail_meta) * 31 + hash(w.trail_tiles)
	if sig == trail_signature:
		return
	trail_signature = sig
	trail_polys = []
	trail_node.mesh = M7TrailBuilder.build(w, trail_polys)
	trails_changed = true

## Which field a plot tile shows: 0 the hoed field, 1 ploughed with the ard (AGR-05), 2 resting under fallow (AGR-07), 3 ploughed
## with the mouldboard (AGR-10). A third of the
## fields of a fallow-keeping civilization rest at a time, and which ones changes every other year.
func field_kind(t: int) -> int:
	var civ: int = w.owner[t]
	if w.has_look(civ, M7WorldData.LOOK_FALLOW):
		var xy := w.tile_xy(t)
		if M7WorldData.hash01(xy.x, xy.y, 313 + w.year / 2) < 0.34:
			return 2
	if w.has_look(civ, M7WorldData.LOOK_MOULDBOARD):
		return 3                  # AGR-10: deep ridge-and-furrow, drawn by a heavy team
	return 1 if w.has_look(civ, M7WorldData.LOOK_ARD) else 0

## Angle of a field's furrows (the field meshes run their rows along local X).
func field_angle(t: int) -> float:
	var xy := w.tile_xy(t)
	return PI / 3.0 * int(M7WorldData.hash01(xy.x, xy.y, 301) * 3.0)

## BLD-08: how far a settlement's ziggurat has come. 0 none, 1 to 3 rising (a third of the labour each), 4 standing.
## The stage, not the permille, goes into the structure digest: the permille changes almost every publish.
static func zig_stage(s: Dictionary) -> int:
	if int(s.get("ziggurats", 0)) > 0:
		return 4
	var pm := int(s.get("zigguratPermille", 0))
	return 0 if pm <= 0 else 1 + mini(2, pm * 3 / 1000)

## Whether a settlement's fire is burning now: the sim lights it only once the civilization has learned to keep fire, and
## clears it when the fire goes out. A village seen from memory shows no fire.
func _fire_lit(s: Dictionary) -> bool:
	return bool(s.get("fireLit", false)) and not bool(s.get("remembered"))

## Whether the fire of camp `ci` is burning: a camp keeps embers from the settlement that sent it out.
func _camp_fire_lit(ci: int) -> bool:
	return ci < w.camp_details.size() and _fire_lit_for_index(int(w.camp_details[ci][3]))

## Whether the fire of the settlement with this sim index (a camp's parent) is burning.
func _fire_lit_for_index(index: int) -> bool:
	for s in w.settlements:
		if int(s.get("index", -1)) == index:
			return _fire_lit(s)
	return false

const FIELD_MESHES := ["field", "field_ard", "field_fallow", "field_plough"]

func _structure_digest() -> int:
	var h := 17
	var flagships := _flagships()
	for si in w.settlements.size():
		var s: Dictionary = w.settlements[si]
		h = h * 31 + int(s.tile)
		var civ := int(s.civ)
		h = h * 31 + civ
		var sea: int = w.sea_tier[civ] if civ >= 0 and civ < w.sea_tier.size() else 0
		h = h * 31 + sea
		h = h * 31 + (w.civ_looks[civ] if civ >= 0 and civ < w.civ_looks.size() else 0)
		if not bool(s.get("remembered")):
			h = h * 31 + _dwelling_signature(s.structures, int(s.get("halls", 0)), int(s.population))
			if sea > 0:
				h = h * 31 + clampi(int(s.population) / 8, 1, 4)
			h = h * 31 + (1 if int(flagships.get(civ, -1)) == si else 0)
			h = h * 31 + zig_stage(s)
			h = h * 31 + mini(int(s.get("temples", 0)), 1)
			h = h * 31 + castle_stage(s) * 7 + w.iron_works(s, "gatehouse") + mini(int(s.get("soldiers", 0)), 6) * 3
			for wid in ["academy", "theatre_arena", "bathhouse", "crane", "aqueduct", "great_dome", "great_library", "great_lighthouse", "press", "windlass_well", "frontier_wall_segment", "galley"]:
				h = h * 31 + w.iron_works(s, wid)
			h = h * 31 + int(s.get("ironBuildKind", -1)) * 3 + mini(2, int(s.get("ironBuildPermille", 0)) * 3 / 1000)
			h = h * 31 + int(s.get("faithForeign", 0)) / 150 + int(s.get("faithOur", 0)) / 150 * 7 + int(s.get("faithCiv", -1)) * 11
			h = h * 31 + int(s.get("provinceSeat", -1)) * 13 + int(s.get("contract", 0)) * 17
			h = h * 31 + int(s.get("facing", 0)) * 19 + hash(w.blockaders_of(int(s.get("index", -1))))
			h = h * 31 + int(s.get("forge", 0)) * 5 + hash(w.siege_of(int(s.get("index", -1))))
		for n in s.structures:
			h = h * 31 + int(n)
		h = h * 31 + (1 if _fire_lit(s) else 0)
		for n in s.get("decor", PackedInt32Array()):
			h = h * 31 + int(n) + 1
	h = h * 31 + hash(w.road_meta) + hash(w.road_tiles) + hash(w.terrace_tiles)
	for i in range(0, w.plots.size(), 2):
		h = h * 31 + w.plots[i] * 7 + w.plots[i + 1] + (field_kind(w.plots[i]) * 3 if w.plots[i + 1] == 0 else 0)
	for ci in w.camps.size():
		var c: Array = w.camps[ci]
		h = h * 31 + int(c[0]) * 7 + int(c[1])
		h = h * 31 + (1 if _camp_fire_lit(ci) else 0)
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
	if mesh_name.begins_with("deposit"):
		return mats.deposit
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

const ZIG_SCALE := 1.3         # drawn larger than life, like the megalith pieces
const ZIG_OFFSET := 0.46       # from the hearth to the ziggurat's centre

## Where a settlement's ziggurat stands: a ring of candidates round the hearth, first one on level, clear ground that is
## not under trees or on the banner. The stair faces the hearth (the city's fire below, the god's fire on top).
func _ziggurat_site(c: Vector2, h0: float, seed: float) -> Variant:
	var banner := c + Vector2(0.09, -0.07)
	for k in 12:
		var a := seed * TAU + PI + k * TAU / 12.0
		var p := c + Vector2(cos(a), sin(a)) * ZIG_OFFSET
		var yaw := atan2((c - p).x, (c - p).y)
		if not _ok_site(p, h0) or p.distance_to(banner) < 0.45:
			continue
		var y := w.height_at(p)
		var ok := true
		for lc in [Vector3(-0.25, 0, -0.215), Vector3(0.25, 0, -0.215), Vector3(0.25, 0, 0.3), Vector3(-0.25, 0, 0.3)]:
			var q3: Vector3 = rot_y(yaw) * (lc * ZIG_SCALE)
			var q := p + Vector2(q3.x, q3.z)
			if not _ok_site(q, h0):
				ok = false
				break
			y = minf(y, w.height_at(q))
		if ok:
			for tp in tree_points:
				if (tp as Vector2).distance_squared_to(p) < 0.36:
					ok = false
					break
		if ok:
			return {"p": p, "y": y, "yaw": yaw}
	return null

const CIVIC := {
	# kind: [mesh, scale, offset from the hearth, half the footprint (x, z) in mesh units, footprint radius in world units]
	"temple": ["temple", 1.3, 0.58, Vector2(0.19, 0.23), 0.36],
	# Iron Age civic works (art_works.gd)
	"academy": ["academy", 1.3, 0.64, Vector2(0.22, 0.2), 0.38],
	"theatre": ["amphitheatre", 1.15, 0.86, Vector2(0.31, 0.31), 0.5],
	"bathhouse": ["bathhouse", 1.3, 0.62, Vector2(0.15, 0.12), 0.28],
	"crane": ["crane", 1.3, 0.58, Vector2(0.1, 0.2), 0.26],
	"site": ["build_site_1", 1.3, 0.62, Vector2(0.21, 0.17), 0.32],
	# the Wonders (art_wonders.gd), and the bigger site one is raised on
	"dome": ["great_dome", 1.35, 1.05, Vector2(0.35, 0.36), 0.62],
	"library": ["great_library", 1.2, 1.0, Vector2(0.52, 0.3), 0.7],
	"wsite": ["build_site_1", 2.1, 1.05, Vector2(0.21, 0.17), 0.6],
	"press": ["press", 1.3, 0.56, Vector2(0.14, 0.11), 0.24],
	"well": ["windlass_well", 1.3, 0.5, Vector2(0.06, 0.06), 0.14],
}
## Iron works drawn as a civic building: the kind here, the id the sim counts it under, and a name for the site that carries it.
const CIVIC_WORKS := [["academy", "academy"], ["theatre", "theatre_arena"], ["bathhouse", "bathhouse"], ["crane", "crane"], ["dome", "great_dome"], ["library", "great_library"], ["press", "press"], ["well", "windlass_well"]]
## Iron works whose building under way is shown as a building site beside the village (the sim's id).
const SITE_KINDS := ["academy", "theatre_arena", "bathhouse", "crane", "aqueduct", "press", "windlass_well", "great_library", "great_lighthouse", "great_dome"]

## Where a civic building stands: rings of candidates round the hearth, first one on level, clear ground that is clear of trees, the
## banner and what is already placed (`placed` and `placed_r`, the latter in village-scale units). The front (+Z) faces the hearth.
func _civic_site(c: Vector2, h0: float, seed: float, kind: String, placed: Array, placed_r: Array) -> Variant:
	var spec: Array = CIVIC[kind]
	var sc: float = spec[1]
	var half: Vector2 = spec[3]
	var foot: float = spec[4]
	var banner := c + Vector2(0.09, -0.07)
	for ring in [1.0, 1.3, 1.6, 1.95, 2.3]:
		for k in 12:
			# north of the hearth first (the default camera then sees the front), then alternately either side of it
			var a := -PI * 0.5 + (((k + 1) / 2) * (1 if k % 2 == 1 else -1)) * TAU / 12.0
			var p: Vector2 = c + Vector2(cos(a), sin(a)) * float(spec[2]) * float(ring)
			var yaw := atan2((c - p).x, (c - p).y)
			if not _ok_site(p, h0) or p.distance_to(banner) < foot + 0.1:
				continue
			var y := w.height_at(p)
			var ok := true
			for lc in [Vector3(-half.x, 0, -half.y), Vector3(half.x, 0, -half.y), Vector3(half.x, 0, half.y), Vector3(-half.x, 0, half.y)]:
				var q3: Vector3 = rot_y(yaw) * (lc * sc)
				var q: Vector2 = p + Vector2(q3.x, q3.z)
				if not _ok_site(q, h0):
					ok = false
					break
				y = minf(y, w.height_at(q))
			if ok:
				for i in placed.size():
					if (placed[i] as Vector2).distance_to(p) < float(placed_r[i]) * VS + foot:
						ok = false
						break
			if ok:
				for tp in tree_points:
					if (tp as Vector2).distance_squared_to(p) < foot * foot:
						ok = false
						break
			if ok:
				return {"p": p, "y": y, "yaw": yaw}
	return null

func _settlements() -> void:
	var K := M7WorldData.Kind
	hearths.clear()
	villages.clear()
	ziggurat_sites.clear()
	castle_sites.clear()
	harbor_sites.clear()
	forge_sites.clear()
	frontier_sites.clear()
	aqueduct_sites.clear()
	beacon_fires.clear()
	lighthouse_sites.clear()
	siege_engines.clear()
	bridge_sites.clear()
	civic_sites.clear()
	var terrace_set := {}
	for tt in w.terrace_tiles:
		terrace_set[int(tt)] = true
	# fields and orchards on their plot tiles
	for pi in range(0, w.plots.size(), 2):
		var t := w.plots[pi]
		if not w.is_land(t):
			continue
		var c := w.tile_center(t)
		var xy := w.tile_xy(t)
		var hv := M7WorldData.hash01(xy.x, xy.y, 301)
		var h: float = w.heights[t]
		if w.plots[pi + 1] == 0 and terrace_set.has(t):
			# AGR-08: a hillside cut into stepped fields behind stone walls, the walls facing downhill (south)
			props.add("terrace_field", _xf(c, h, 0.0, 1.0), Color(1, 1, 1, hv))
		elif w.plots[pi + 1] == 0:
			props.add(FIELD_MESHES[field_kind(t)], _xf(c, h, PI / 3.0 * int(hv * 3.0) + PI / 6.0 * 0.0, 1.0), Color(1, 1, 1, hv))
		elif w.has_look(int(w.owner[t]), M7WorldData.LOOK_GRAFTING) and hv < 0.45:
			# FOR-08: a grafted plot is a trellised vineyard
			props.add("vineyard", _xf(c, h, PI / 3.0 * int(hv * 6.0), 1.0), Color(1, 1, 1, hv))
		else:
			for i in 7:
				var p := c if i == 6 else c + Vector2(cos(TAU * i / 6.0), sin(TAU * i / 6.0)) * 0.27
				var hv2 := M7WorldData.hash01(xy.x + i, xy.y, 303)
				var grove := "olive_tree" if w.has_look(int(w.owner[t]), M7WorldData.LOOK_GRAFTING) else "orchard_tree"      # FOR-08: an olive grove
				props.add(grove, _xf(p, h, hv2 * TAU, 0.9 + 0.3 * hv2), _tint(hv2))
			for i in 6:
				var p := _rand_in_hex(t, 40 + i, 0.9)
				props.add("tuft0", _xf(p, h, i * 1.3, 1.0), _tint(0.5))

	var flagship := _flagships()
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
			if _fire_lit(s):
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
		var landmarks_seed := []
		# the ziggurat (BLD-08) takes the heart of the city: houses spiral round it, the stair faces the hearth
		var zs := zig_stage(s)
		var zsite = _ziggurat_site(c, h0, seed) if zs > 0 else null
		if zsite != null:
			placed.append(zsite.p)
			placed_r.append(0.36)
			landmarks_seed.append([zsite.p, 0.8])
			var zp: Vector2 = zsite.p
			props.add(M7MeshKit.ZIG_MESHES[zs - 1], _xf(zp, zsite.y, zsite.yaw, ZIG_SCALE),
				Color(cc.r, cc.g, cc.b, M7WorldData.hash01(t, 0, 91)))
			if zs == 4:
				var o: Vector3 = rot_y(zsite.yaw) * (Vector3(-0.056, 0.314, -0.09) * ZIG_SCALE)
				props.add("banner%d" % civ, _xf(zp + Vector2(o.x, o.z), zsite.y + o.y, -0.4, 1.0), Color(cc.r, cc.g, cc.b, seed))
			ziggurat_sites.append({"p": zp, "y": zsite.y, "yaw": zsite.yaw, "stage": zs, "civ": civ, "seed": seed, "tile": t})
			decor_spots["ziggurat"] = zp
		# the temple (BEL-06), one drawn, beside the ziggurat when the city has both
		if int(s.get("temples", 0)) > 0:
			var tsite = _civic_site(c, h0, seed, "temple", placed, placed_r)
			if tsite != null:
				var spec: Array = CIVIC["temple"]
				var tp: Vector2 = tsite.p
				var temple_mesh: String = "temple_tier2" if w.has_look(civ, M7WorldData.LOOK_COLONNADES) else String(spec[0])   # STN-12: a colonnade
				props.add(temple_mesh, _xf(tp, tsite.y, tsite.yaw, spec[1]), Color(cc.r, cc.g, cc.b, M7WorldData.hash01(t, 0, 92)))
				placed.append(tp)
				placed_r.append(float(spec[4]) / VS)
				landmarks_seed.append([tp, 0.6])
				civic_sites.append({"kind": "temple", "p": tp, "y": tsite.y, "yaw": tsite.yaw, "civ": civ, "seed": seed, "tile": t})
				decor_spots["temple_built"] = tp
		# Iron civic works (academy, theatre or arena, bathhouse, crane): each stands on the best level ground left round the hearth
		for cw in CIVIC_WORKS:
			if w.iron_works(s, String(cw[1])) > 0:
				_place_civic(String(cw[0]), s, t, c, h0, seed, placed, placed_r, civ, cc)
		# a building going up (own towns only, as the sim sends it): its site, in stages
		var build_k := int(s.get("ironBuildKind", -1))
		if build_k >= 0 and build_k < w.iron_work_ids.size() and SITE_KINDS.has(String(w.iron_work_ids[build_k])):
			var pm := int(s.get("ironBuildPermille", 0))
			if pm > 0:
				var wonder := String(w.iron_work_ids[build_k]).begins_with("great_")
				_place_civic("wsite" if wonder else "site", s, t, c, h0, seed, placed, placed_r, civ, cc, 1 + mini(2, pm * 3 / 1000))
		var slot := 0
		var rmax := 0.0
		var tiled_roofs := civ >= 0 and w.has_look(civ, M7WorldData.LOOK_TILES)
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
			# CER-08 Fired Brick & Tile: wattle, mudbrick and long houses wear terracotta tiles
			var shown_mesh: String = it + "_tile" if tiled_roofs and it in TILED_HOUSES else it
			props.add(shown_mesh, _xf(p, w.height_at(p), ang, sc), Color(cc.r, cc.g, cc.b, M7WorldData.hash01(t, placed.size(), 6)))
		if zsite != null:
			rmax = maxf(rmax, ZIG_OFFSET + 0.3)
		for cs in civic_sites:
			if int(cs.tile) == t:
				rmax = maxf(rmax, ((cs.p as Vector2) - c).length() + 0.4)
		villages.append(Vector3(c.x, c.y, rmax * 0.75 + 0.08))
		var sea: int = w.sea_tier[civ] if civ >= 0 and civ < w.sea_tier.size() else 0
		# a harbor (SEA-07): quay and piers on the shore, the boats moored between the piers
		var has_harbor := w.iron_works(s, "harbor") > 0 and _harbor(s, t, c0, seed, cc)
		_lighthouse(s, t, c0, seed)
		if sea > 0:
			_moor_fleet(t, c0, seed, sea, int(s.population), cc, has_harbor)
		var decor := _village_decor(s.get("decor", PackedInt32Array()), civ, si, flagship)
		var decor_extent := 0.0
		if decor.size() > 0:
			decor_extent = _place_decor(decor, t, c, c0, h0, seed, rmax, placed, cc, st[K.THORN_BARRIER] > 0, landmarks_seed, int(st[K.STONE_WALLS]) > 0)
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
		# stone walls (DEF-05) ring the village, a gatehouse (DEF-06) towers over its gate, and a castle (DEF-08) holds the ground outside
		var wall_rings := int(st[K.STONE_WALLS])
		var wall_base := rmax
		var gate_pref := seed * TAU
		if zsite != null:
			# the ziggurat is wider than the village's own reach: the ring stands outside it, and its gate faces away from it
			wall_base = maxf(rmax, ZIG_OFFSET + 0.55)
			var zd: Vector2 = (zsite.p as Vector2) - c
			gate_pref = atan2(zd.y, zd.x) + PI
		# an Iron forge yard (MET-09) and the garrison's drill ground stand inside the ring too
		decor_extent = maxf(decor_extent, _forge_yard(s, c, h0, seed, rmax, placed))
		decor_extent = maxf(decor_extent, _drill(s, c, h0, seed, rmax, placed, civ))
		decor_extent = maxf(decor_extent, _faith_standards(s, c, h0, seed, rmax, placed))
		decor_extent = maxf(decor_extent, _province_standards(s, si, c, h0, seed, rmax, placed, cc))
		# the ring stands round the houses and the landmarks and near props that were laid out with them
		var ring_r := maxf(wall_base + WALL_RING, decor_extent + 0.14)
		var siege := _siege_info(s, c, seed)
		var gate_at := Vector2(1e9, 1e9)
		if wall_rings > 0:
			gate_at = _stone_walls(t, c, seed, ring_r, wall_rings, w.iron_works(s, "gatehouse") > 0, gate_pref, siege)
		_aqueduct(s, c, h0, seed, ring_r if wall_rings > 0 else wall_base, gate_at)
		_frontier_walls(s, si, c, h0, seed, ring_r if wall_rings > 0 else wall_base)
		if not siege.is_empty():
			_siege_camp(c, seed, ring_r if wall_rings > 0 else wall_base + 0.2, siege, cc)
		_castle(s, t, c, h0, seed, (ring_r + CASTLE_GAP_WALLED) if wall_rings > 0 else wall_base + CASTLE_GAP_OPEN, placed, cc, gate_at)
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
				# wool breeds (HUS-06) fill the paddock: a bigger flock of fuller fleeces
				var wool := w.has_look(civ, M7WorldData.LOOK_WOOL)
				for j in (10 if wool else 4):
					var q := pc + Vector2(cos(j * 1.7 + seed), sin(j * 1.7 + seed)) * (0.06 + 0.04 * j)
					if wool:
						q = pc + Vector2(cos(j * 2.39996 + seed), sin(j * 2.39996 + seed)) * (0.045 * sqrt(j + 0.6))
					var dark := j == 2 or (wool and j == 7)
					props.add("animal_woolly", _xf(q, ph, j * 2.1 + seed, 1.14 if wool else 1.0),
						_glide_custom((6 * 8 + 4) if not dark else (9 * 8 + 3), 0, 0.5, 0.0, Vector2.ZERO))
				break

	_bridges()

## Dwellings a village shows: what it has built, newest first, not an even mix of everything it ever knew. Outgrown types
## (lean-tos and hide tents once there are permanent houses, and so on up the ladder) are drawn no more; the people the built houses
## do not cover live in the best small house the village knows (or in more longhouses when that is all it has). Generic open-sleeping
## placeholders use the small windbreak; a researched Hide Tent has its own, clearly recognizable mesh.
const TILED_HOUSES := ["wattle", "mudbrick", "longhouse"]
const DWELL_CAPACITY := {"leanto": 4, "tent": 5, "hide_tent": 5, "pithouse": 6, "wattle": 8, "mudbrick": 8, "longhouse": 25, "hall": 30}
const DWELL_RADIUS := {"leanto": 0.085, "tent": 0.08, "hide_tent": 0.08, "pithouse": 0.105, "wattle": 0.1, "mudbrick": 0.095, "longhouse": 0.165, "hall": 0.2}
const DWELL_MAX_EACH := 10
const DWELL_MAX_TOTAL := 36

func _dwelling_signature(st: Array, halls: int, pop: int) -> int:
	var K := M7WorldData.Kind
	var built_hide_tent := int(st[K.HIDE_TENT])
	var built_leanto := int(st[K.LEAN_TO])
	var built_pithouse := int(st[K.PIT_HOUSE])
	var built_wattle := int(st[K.WATTLE_HOUSE])
	var built_mudbrick := int(st[K.MUDBRICK_HOUSE])
	var built_longhouse := int(st[K.LONGHOUSE])
	var shown_hall := mini(halls, DWELL_MAX_EACH)
	var shown_longhouse := mini(built_longhouse, DWELL_MAX_EACH)
	var shown_mudbrick := mini(built_mudbrick, DWELL_MAX_EACH)
	var shown_wattle := mini(built_wattle, DWELL_MAX_EACH)
	var shown_pithouse := mini(built_pithouse, DWELL_MAX_EACH)
	var shown_hide_tent := mini(built_hide_tent, DWELL_MAX_EACH)
	var shown_leanto := mini(built_leanto, DWELL_MAX_EACH)
	if built_pithouse + built_wattle + built_mudbrick + built_longhouse > 0:
		shown_hide_tent = 0
		shown_leanto = 0
	if built_wattle + built_mudbrick + built_longhouse > 0:
		shown_pithouse = 0
	if built_mudbrick + built_longhouse > 0:
		shown_wattle = 0
	var covered := shown_hall * int(DWELL_CAPACITY.hall) + shown_longhouse * int(DWELL_CAPACITY.longhouse) \
		+ shown_mudbrick * int(DWELL_CAPACITY.mudbrick) + shown_wattle * int(DWELL_CAPACITY.wattle) \
		+ shown_pithouse * int(DWELL_CAPACITY.pithouse) + shown_hide_tent * int(DWELL_CAPACITY.hide_tent) \
		+ shown_leanto * int(DWELL_CAPACITY.leanto)
	var short := maxi(0, pop - covered)
	var extra_type := ""
	var extra_count := 0
	if short > 0:
		var filler := "leanto" if built_leanto > 0 else "tent"
		if built_hide_tent > 0:
			filler = "hide_tent"
		if built_pithouse > 0:
			filler = "pithouse"
		if built_wattle > 0:
			filler = "wattle"
		if built_mudbrick > 0:
			filler = "mudbrick"
		if (filler == "tent" or filler == "leanto" or filler == "hide_tent") and built_longhouse > 0 and built_pithouse + built_wattle + built_mudbrick == 0:
			filler = "longhouse"
		extra_type = filler
		extra_count = mini(ceili(float(short) / float(DWELL_CAPACITY[filler])), maxi(0, DWELL_MAX_TOTAL \
			- shown_hall - shown_longhouse - shown_mudbrick - shown_wattle - shown_pithouse - shown_hide_tent - shown_leanto))
	if shown_hall + shown_longhouse + shown_mudbrick + shown_wattle + shown_pithouse + shown_hide_tent + shown_leanto + extra_count == 0 and pop > 0:
		extra_type = "tent"
		extra_count = 1
	var signature := 17
	for count in [shown_hall, shown_longhouse, shown_mudbrick, shown_wattle, shown_pithouse, shown_hide_tent, shown_leanto]:
		signature = signature * 31 + count
	signature = signature * 31 + (hash(extra_type) if extra_count > 0 else 0)
	signature = signature * 31 + extra_count
	return signature

func _item_radius(mesh_name: String) -> float:
	return DWELL_RADIUS.get(mesh_name, 0.075)

func _dwellings(st: Array, halls: int, pop: int) -> Array:
	var K := M7WorldData.Kind
	var built := {
		"leanto": int(st[K.LEAN_TO]), "tent": 0, "hide_tent": int(st[K.HIDE_TENT]), "pithouse": int(st[K.PIT_HOUSE]),
		"wattle": int(st[K.WATTLE_HOUSE]), "mudbrick": int(st[K.MUDBRICK_HOUSE]), "longhouse": int(st[K.LONGHOUSE]),
		"hall": halls}
	# The first shelter is a lean-to (BLD-01); BLD-02's crafted hide tent has a distinct silhouette from the generic placeholder.
	var filler := "leanto" if built.leanto > 0 else "tent"
	if built.hide_tent > 0:
		filler = "hide_tent"
	for nm in ["pithouse", "wattle", "mudbrick"]:
		if built[nm] > 0:
			filler = nm
	var shown := {}
	for nm in built:
		shown[nm] = mini(int(built[nm]), DWELL_MAX_EACH)
	if built.pithouse + built.wattle + built.mudbrick + built.longhouse > 0:
		shown.leanto = 0
		shown.hide_tent = 0
	if built.wattle + built.mudbrick + built.longhouse > 0:
		shown.pithouse = 0
	if built.mudbrick + built.longhouse > 0:
		shown.wattle = 0
	var covered := 0
	for nm in shown:
		covered += int(shown[nm]) * int(DWELL_CAPACITY[nm])
	var short := maxi(0, pop - covered)
	var out := []
	for nm in ["hall", "longhouse", "mudbrick", "wattle", "pithouse", "hide_tent", "tent", "leanto"]:
		for i in int(shown[nm]):
			out.append(nm)
	if short > 0:
		if (filler == "tent" or filler == "leanto" or filler == "hide_tent") and built.longhouse > 0 and built.pithouse + built.wattle + built.mudbrick == 0:
			filler = "longhouse"
		var more := ceili(float(short) / float(DWELL_CAPACITY[filler]))
		for i in more:
			if out.size() >= DWELL_MAX_TOTAL:
				break
			out.append(filler)
	if out.is_empty() and pop > 0:
		out.append("tent")
	return out

## Kinds that sit on the village edge rather than among the houses.
const DECOR_FAR := ["cairn", "shrine", "signal", "charcoal", "grave", "quarry", "skypole"]
## Landmarks need room: the clear distance their spot keeps from every house and prop.
const DECOR_CLEAR := {"temple": 0.42, "chiefhall": 0.42}

## The greatest village of each civilization (by people, then by tile): where its temple and chief's hall stand.
func _flagships() -> Dictionary:
	var best := {}
	for si in w.settlements.size():
		var s: Dictionary = w.settlements[si]
		if bool(s.get("remembered")) or int(s.population) <= 0 or int(s.civ) < 0:
			continue
		var civ := int(s.civ)
		if not best.has(civ) or int(s.population) > int(w.settlements[best[civ]].population):
			best[civ] = si
	return best

## The props a village shows: what the focus civilization's knowledge earned (DecorTable.cs, passed from the host) and what any
## civilization's looks add (CivLook bits): fleece bales (HUS-06), a parked chariot (WAR-06) and, in its greatest village, the
## stepped temple (BEL-07) and the chief's hall (SOC-07). Landmarks come first so they get the best ground.
func _village_decor(host_decor, civ: int, si: int, flagship: Dictionary) -> Array:
	var out := []
	var names: Array = M7MeshKit.DECOR_NAMES
	if int(flagship.get(civ, -1)) == si:
		if w.has_look(civ, M7WorldData.LOOK_STATE_CULT):
			out.append(names.find("temple"))
		if w.has_look(civ, M7WorldData.LOOK_CHIEFDOM):
			out.append(names.find("chiefhall"))
	for k in host_decor:
		out.append(int(k))
	if w.has_look(civ, M7WorldData.LOOK_WOOL):
		out.append(names.find("woolbales"))
	if w.has_look(civ, M7WorldData.LOOK_CHARIOTS):
		out.append(names.find("chariot"))
	return out

## Props a village earns from what its people know (see DecorTable.cs). Placed on an outward spiral clear of the houses; the
## palisade rings the village, `placed` holds the house spots so nothing overlaps.
func _place_decor(decor, t: int, c: Vector2, c0: Vector2, h0: float, seed: float, rmax: float, placed: Array, cc: Color,
		has_thorns: bool, landmarks_seed: Array = [], walled: bool = false) -> float:
	# A stone wall ring (DEF-05) takes the place of the palisade. Houses and the near props and landmarks stay inside it (the ring is drawn
	# round the farthest of them, see the return value); the far props (cairns, shrines, quarries ...) stand outside it.
	var near_r := rmax + 0.17
	var far_r := rmax + 0.3 + (WALL_CLEAR if walled else 0.0)
	var extent := 0.0
	var slot := 0
	var landmarks := landmarks_seed.duplicate()        # [spot, clear distance] of the big buildings placed so far
	for kind in decor:
		var kind_i := int(kind)
		if kind_i < 0 or kind_i >= M7MeshKit.DECOR_NAMES.size():
			continue
		var decor_name := String(M7MeshKit.DECOR_NAMES[kind_i])
		var mesh_name := "decor_" + decor_name
		var tint := Color(cc.r, cc.g, cc.b, M7WorldData.hash01(t, kind_i, 11))
		if decor_name == "palisade":
			if walled:
				continue
			_decor_palisade(t, c, seed, rmax + (0.2 if has_thorns else 0.13), tint)
			continue
		var base := far_r if DECOR_FAR.has(decor_name) else near_r
		var need: float = DECOR_CLEAR.get(decor_name, 0.17)
		if need > 0.2:
			base = rmax + need * 0.55
		var found := false
		var p := Vector2.ZERO
		for k in 40:
			var a := seed * TAU + (slot + k) * 2.39996
			var q := c + Vector2(cos(a), sin(a)) * (base + 0.035 * (k / 8))
			if not _ok_site(q, h0):
				continue
			var clear := true
			for pp in placed:
				if (pp as Vector2).distance_to(q) < need:
					clear = false
					break
			for lm in landmarks:
				if (lm[0] as Vector2).distance_to(q) < maxf(need, float(lm[1]) * 0.8):
					clear = false
					break
			if clear:
				p = q
				found = true
				break
		slot += 3
		if not found:
			continue
		if need > 0.2:
			landmarks.append([p, need])
		placed.append(p)
		decor_spots[decor_name] = p
		if not DECOR_FAR.has(decor_name):
			extent = maxf(extent, p.distance_to(c) + (need * 0.5 if need > 0.2 else 0.1))
		var d := c - p
		var ang := atan2(d.x, d.y)
		var sc := VS * (0.95 + 0.12 * M7WorldData.hash01(t, kind_i, 12))
		var y := w.height_at(p)
		props.add(mesh_name, _xf(p, y, ang, sc), tint)
		if decor_name == "signal":
			props.add("flame", _xf(p, y + 0.05 * sc, ang, 0.8), Color(1, 1, 1, seed))
	return extent

const FORGE_SCALE := 1.2

## A civic building of the Iron Age (art_works.gd) at a free spot by the hearth, its front toward it. `stage` picks the building site's mesh (1 to 3).
func _place_civic(kind: String, s: Dictionary, t: int, c: Vector2, h0: float, seed: float, placed: Array, placed_r: Array, civ: int, cc: Color,
		stage: int = 0) -> void:
	var site = _civic_site(c, h0, seed, kind, placed, placed_r)
	if site == null:
		return
	var spec: Array = CIVIC[kind]
	var mesh_name: String = spec[0] if stage <= 0 else "build_site_%d" % stage
	var tp: Vector2 = site.p
	props.add(mesh_name, _xf(tp, site.y, site.yaw, spec[1]), Color(cc.r, cc.g, cc.b, M7WorldData.hash01(t, kind.length(), 93)))
	placed.append(tp)
	placed_r.append(float(spec[4]) / VS)
	civic_sites.append({"kind": kind, "p": tp, "y": site.y, "yaw": site.yaw, "civ": civ, "seed": seed, "tile": t})

const LIGHTHOUSE_SCALE := 1.1

## The Great Lighthouse (SEA-08): on the shore of the first water neighbour (the one the harbor and the boats use), a little way along it from the harbor, with a
## fire in its lantern (the war rig). Nothing if there is no shore.
func _lighthouse(s: Dictionary, t: int, c0: Vector2, seed: float) -> void:
	if w.iron_works(s, "great_lighthouse") <= 0:
		return
	for i in 6:
		var nb: int = w.neighbour(t, (i + int(seed * 6.0)) % 6)
		if nb < 0 or not w.is_water(nb):
			continue
		var dir: Vector2 = (w.tile_center(nb) - c0).normalized()
		var tangent := dir.rotated(PI * 0.5)
		for sgn in [1.0, -1.0]:
			var p: Vector2 = c0 + dir * 0.4 + tangent * 0.95 * sgn
			var tq := w.tile_at(p)
			if tq < 0 or not w.is_land(tq):
				continue
			var y := maxf(w.height_at(p), 0.05)
			props.add("great_lighthouse", _xf(p, y, 0.0, LIGHTHOUSE_SCALE), Color(1, 1, 1, seed))
			beacon_fires.append({"top": Vector3(p.x, y + 0.74 * LIGHTHOUSE_SCALE, p.y), "seed": seed})
			lighthouse_sites.append({"p": p, "y": y})
			return
		return

const AQUEDUCT_SCALE := 1.3

## The aqueduct (BLD-10): a water tower inside the village edge and four spans of arched channel running out from it, leveled over uneven ground (the arches
## stand taller over dips), toward the nearest river when there is one. The line keeps off the gate, the castle and the civic buildings.
func _aqueduct(s: Dictionary, c: Vector2, h0: float, seed: float, base_r: float, gate_at: Vector2) -> void:
	if w.iron_works(s, "aqueduct") <= 0:
		return
	var pref := seed * TAU + 1.0
	var river := _river_near(c)
	if float(river[0]) < 4.0:
		var d: Vector2 = (river[1] as Vector2) - c
		if d.length() > 0.01:
			pref = atan2(d.y, d.x)
	var spans := 4
	var half_end := 0.07 * AQUEDUCT_SCALE
	var run := 0.3 * AQUEDUCT_SCALE
	var chosen := INF
	for k in 12:
		var a := pref + TAU / 12.0 * (((k + 1) / 2) * (1 if k % 2 == 1 else -1))
		var dir := Vector2(cos(a), sin(a))
		var ok := true
		for j in spans + 1:
			var p := c + dir * (base_r + half_end + run * (j - 0.5 if j > 0 else -0.3))
			var tq := w.tile_at(p)
			if tq < 0 or not w.is_land(tq) or w.biome[tq] >= M7WorldData.Biome.MOUNTAIN:
				ok = false
				break
			if gate_at.distance_to(p) < 0.55:
				ok = false
				break
			for cs in castle_sites:
				if (cs.p as Vector2).distance_to(p) < 0.6:
					ok = false
			for cv in civic_sites:
				if int(cv.tile) == int(w.tile_at(c)) and (cv.p as Vector2).distance_to(p) < 0.45:
					ok = false
		if ok:
			chosen = a
			break
	if chosen == INF:
		return
	var dir := Vector2(cos(chosen), sin(chosen))
	var yaw := -chosen
	# the deck's level: above the highest ground under the line
	var grounds: Array = []
	var level := w.height_at(c + dir * base_r)
	var end_p := c + dir * (base_r - 0.04)
	grounds.append(w.height_at(end_p))
	for j in spans:
		var p := c + dir * (base_r + half_end + run * (j + 0.5))
		grounds.append(w.height_at(p))
	for g in grounds:
		level = maxf(level, float(g))
	var deck := level + 0.16 * AQUEDUCT_SCALE
	var end_sy := clampf((deck - float(grounds[0])) / (0.15 * AQUEDUCT_SCALE), 0.6, 4.0)
	props.add("aqueduct_end", Transform3D(Basis(Vector3.UP, yaw) * Basis.from_scale(Vector3(AQUEDUCT_SCALE, AQUEDUCT_SCALE * end_sy, AQUEDUCT_SCALE)),
		Vector3(end_p.x, float(grounds[0]), end_p.y)), Color(1, 1, 1, seed))
	for j in spans:
		var p := c + dir * (base_r + half_end + run * (j + 0.5))
		var gy: float = grounds[j + 1]
		var sy := clampf((deck - gy) / (0.14 * AQUEDUCT_SCALE), 0.5, 4.0)
		props.add("aqueduct_run", Transform3D(Basis(Vector3.UP, yaw) * Basis.from_scale(Vector3(AQUEDUCT_SCALE, AQUEDUCT_SCALE * sy, AQUEDUCT_SCALE)),
			Vector3(p.x, gy, p.y)), Color(1, 1, 1, seed))
	aqueduct_sites.append({"p": end_p + dir * 0.6, "y": float(grounds[0])})

## Frontier wall segments (DEF-07): a straight run of wall with towers at its ends, out in the claim on the side facing the civilization each segment answers
## (the nearest settlement of that civilization), one run for each such civilization, as long as the number of segments.
func _frontier_walls(s: Dictionary, si: int, c: Vector2, h0: float, seed: float, base_r: float) -> void:
	var mask := int(s.get("facing", 0))
	var segments := w.iron_works(s, "frontier_wall_segment")
	if segments <= 0:
		return
	var civ_self := int(s.civ)
	for foe in 8:
		if (mask >> foe) & 1 == 0 and mask != 0:
			continue
		if mask == 0 and foe != 0:
			break
		# the direction of the nearest settlement of the faced civilization (or of any other civilization when the facing is not known)
		var best := 1e9
		var dir := Vector2.ZERO
		for other in w.settlements:
			var oc := int(other.civ)
			if oc == civ_self or (mask != 0 and oc != foe):
				continue
			var d: Vector2 = w.tile_center(int(other.tile)) - c
			if d.length() < best and d.length() > 0.1:
				best = d.length()
				dir = d.normalized()
		if dir == Vector2.ZERO:
			continue
		var mid := c + dir * (base_r + 1.35)
		var tangent := dir.rotated(PI * 0.5)
		var n := clampi(2 + segments * 2, 3, 9)
		var yaw := -atan2(tangent.y, tangent.x)
		var seg_len := ArtDefense.SEG * WALL_SCALE
		for k in n:
			var p := mid + tangent * ((k - (n - 1) * 0.5) * seg_len * 0.97)
			var tq := w.tile_at(p)
			if tq < 0 or not w.is_land(tq) or _river_dist(p) < 0.08:
				continue
			props.add("wall_seg", Transform3D(Basis(Vector3.UP, yaw) * Basis.from_scale(Vector3(0.97, WALL_SCALE, WALL_SCALE)), Vector3(p.x, w.height_at(p), p.y)),
				Color(1, 1, 1, seed))
		for end_k: int in [0, n - 1]:
			var tp: Vector2 = mid + tangent * ((float(end_k) - (n - 1) * 0.5) * seg_len * 0.97)
			var ttq := w.tile_at(tp)
			if ttq >= 0 and w.is_land(ttq):
				props.add("wall_tower", Transform3D(Basis(Vector3.UP, yaw) * Basis.from_scale(Vector3.ONE * WALL_SCALE), Vector3(tp.x, w.height_at(tp), tp.y)), Color(1, 1, 1, seed))
		frontier_sites.append({"p": mid, "y": w.height_at(mid)})

## An Iron forge yard (MET-09) at the edge of the village, its front toward the hearth: the bloomery stack, anvil, charcoal and ore, and, when the forge is at
## work, a glowing furnace mouth with flame and smoke (the war rig). Returns how far from the hearth it reaches, 0 with no forge.
func _forge_yard(s: Dictionary, c: Vector2, h0: float, seed: float, rmax: float, placed: Array) -> float:
	var state := int(s.get("forge", 0))
	if state <= 0:
		return 0.0
	for k in 36:
		var a := seed * TAU + 1.3 + k * 2.39996
		var q := c + Vector2(cos(a), sin(a)) * (rmax + 0.2 + 0.03 * (k / 12))
		if not _ok_site(q, h0):
			continue
		var clear := true
		for pp in placed:
			if (pp as Vector2).distance_to(q) < 0.36:
				clear = false
				break
		if not clear:
			continue
		var yaw := atan2((c - q).x, (c - q).y)
		var y := w.height_at(q)
		props.add("forge_yard_hot" if state >= 2 else "forge_yard", _xf(q, y, yaw, FORGE_SCALE), Color(1, 1, 1, seed))
		placed.append(q)
		if state >= 2:
			var rot := rot_y(yaw)
			var chimney: Vector3 = rot * (Vector3(0, 0.185, -0.05) * FORGE_SCALE)
			var mouth: Vector3 = rot * (Vector3(0, 0.04, 0.014) * FORGE_SCALE)
			forge_sites.append({"chimney": Vector3(q.x + chimney.x, y + chimney.y, q.y + chimney.z),
				"mouth": Vector3(q.x + mouth.x, y + mouth.y, q.y + mouth.z), "seed": seed})
		return q.distance_to(c) + 0.26
	return 0.0

## A free spot at the village edge, clear of what is placed, for a small prop: tried round the edge from `phase`. `null` when there is none.
func _edge_spot(c: Vector2, h0: float, seed: float, rmax: float, placed: Array, phase: float, clear: float = 0.2) -> Variant:
	for k in 60:
		var a := seed * TAU + phase + k * 2.39996
		var q := c + Vector2(cos(a), sin(a)) * (rmax + 0.14 + 0.07 * (k / 12))
		if not _ok_site(q, h0):
			continue
		var free := true
		for pp in placed:
			if (pp as Vector2).distance_to(q) < clear:
				free = false
				break
		if free:
			return q
	return null

## The standards of a spreading faith (BEL-09): a cairn with a tall pennant in the faith's colour for each share of the town that follows it, 20, 45 and
## 70 per cent. In a town of the viewer's own it is the strongest foreign faith there; in a rival's town the viewer sees, the viewer's faith. Returns how far
## it reaches.
func _faith_standards(s: Dictionary, c: Vector2, h0: float, seed: float, rmax: float, placed: Array) -> float:
	var permille := 0
	var faith_civ := -1
	if int(s.get("faithForeign", 0)) > 0:
		permille = int(s.faithForeign)
		faith_civ = int(s.get("faithCiv", -1))
	elif int(s.get("faithOur", 0)) > 0:
		permille = int(s.faithOur)
		faith_civ = w.viewer_civ
	if faith_civ < 0 or permille < 200:
		return 0.0
	var count := 1 + (1 if permille >= 450 else 0) + (1 if permille >= 700 else 0)
	var spot = _edge_spot(c, h0, seed, rmax, placed, 2.2, 0.26)
	if spot == null:
		return 0.0
	var q: Vector2 = spot
	var tint := M7MeshBuilder.lin(M7WorldData.civ_color(faith_civ), 0.5)
	var face := atan2((c - q).x, (c - q).y)
	var across := Vector2(cos(face), -sin(face))
	for i in count:
		var p := q + across * ((i - (count - 1) * 0.5) * 0.09)
		props.add("faith_standard", _xf(p, w.height_at(p), face, 1.3), Color(tint.r, tint.g, tint.b, M7WorldData.hash01(i, 5, 94)))
	placed.append(q)
	return q.distance_to(c) + 0.12

## A province seat's standard (TER-08) and the banner of the feudal contract (GOV-09), in the owner's colour, at the village edge: for the viewer's own towns.
func _province_standards(s: Dictionary, si: int, c: Vector2, h0: float, seed: float, rmax: float, placed: Array, cc: Color) -> float:
	var extent := 0.0
	var seat := int(s.get("provinceSeat", -1))
	if seat >= 0 and seat == si:
		var spot = _edge_spot(c, h0, seed, rmax, placed, 0.7, 0.24)
		if spot != null:
			var q: Vector2 = spot
			props.add("province_standard", _xf(q, w.height_at(q), atan2((c - q).x, (c - q).y), 1.3), Color(cc.r, cc.g, cc.b, seed))
			placed.append(q)
			extent = q.distance_to(c) + 0.12
	if int(s.get("contract", 0)) > 0:
		var spot2 = _edge_spot(c, h0, seed, rmax, placed, 3.1, 0.22)
		if spot2 != null:
			var q2: Vector2 = spot2
			props.add("feudal_banner", _xf(q2, w.height_at(q2), atan2((c - q2).x, (c - q2).y), 1.3), Color(cc.r, cc.g, cc.b, seed))
			placed.append(q2)
			extent = maxf(extent, q2.distance_to(c) + 0.1)
	return extent

## The soldiers posted in a village of the viewer's own (WAR-07): up to six iron-armed soldiers drawn up in two ranks on a drill ground at the edge, facing the
## hearth. Returns how far it reaches, 0 with none.
func _drill(s: Dictionary, c: Vector2, h0: float, seed: float, rmax: float, placed: Array, civ: int) -> float:
	var count := mini(int(s.get("soldiers", 0)), 6)
	if count <= 0:
		return 0.0
	for k in 36:
		var a := seed * TAU + 4.1 + k * 2.39996
		var q := c + Vector2(cos(a), sin(a)) * (rmax + 0.16 + 0.03 * (k / 12))
		if not _ok_site(q, h0):
			continue
		var clear := true
		for pp in placed:
			if (pp as Vector2).distance_to(q) < 0.3:
				clear = false
				break
		if not clear:
			continue
		var face := atan2((c - q).x, (c - q).y)
		var across := Vector2(cos(face), -sin(face))    # to the soldiers' right
		var fwd := (c - q).normalized()
		for i in count:
			var rank := i / 3
			var file := i % 3 - 1
			var p := q + across * (file * 0.1) - fwd * (rank * 0.1)
			props.add("person_soldier", _xf(p, w.height_at(p), face, 1.0), Color(float(civ) + 0.5 * 0.98, 0.0, 0.0, 0.0))
		placed.append(q)
		return q.distance_to(c) + 0.2
	return 0.0

## The siege laid on a town (WAR-09), from the sim's row: the way the engines stand (toward the besieger's host when known, else a way the seed picks),
## the wall's integrity, the engines and the besieger. Empty when the town is not besieged.
func _siege_info(s: Dictionary, c: Vector2, seed: float) -> Dictionary:
	var row := w.siege_of(int(s.get("index", -1)))
	if row.is_empty():
		return {}
	var angle := seed * TAU + PI
	var origin := int(row[4])
	if origin >= 0 and origin < w.width * w.height:
		var d := w.tile_center(origin) - c
		if d.length() > 0.01:
			angle = atan2(d.y, d.x)
	return {"angle": angle, "integrity": int(row[3]), "engines": int(row[1]), "civ": int(row[0])}

## The besieger's camp: up to four counterweighted engines in an arc outside the wall, facing it (their arms and shot are the war rig's), a stack of shot beside
## each, tents behind and the besieger's banner.
func _siege_camp(c: Vector2, seed: float, ring: float, siege: Dictionary, cc: Color) -> void:
	var angle: float = siege.angle
	var count := clampi(int(siege.engines), 1, 4)
	var civ := int(siege.civ)
	var tint := M7MeshBuilder.lin(M7WorldData.civ_color(civ), 0.5)
	for k in count:
		var spread := (k - (count - 1) * 0.5) * 0.3
		var dir := Vector2(cos(angle + spread), sin(angle + spread))
		var p := c + dir * (ring + 0.75)
		var tq := w.tile_at(p)
		if tq < 0 or not w.is_land(tq):
			continue
		var y := w.height_at(p)
		var yaw := atan2((c - p).x, (c - p).y)
		props.add("siege_base", Transform3D(Basis(Vector3.UP, yaw) * Basis.from_scale(Vector3.ONE * 1.2), Vector3(p.x, y, p.y)), Color(1, 1, 1, 0.5))
		var impact := c + dir * ring
		siege_engines.append({"p": Vector3(p.x, y, p.y), "yaw": yaw, "target": Vector3(impact.x, w.height_at(impact) + 0.1, impact.y),
			"seed": M7WorldData.hash01(int(p.x * 100.0), int(p.y * 100.0), 81), "k": k})
		var side := Vector2(-dir.y, dir.x)
		for j in 3:
			var sp := p + side * (0.12 + 0.03 * j) - dir * 0.04 * j
			props.add("siege_stone", Transform3D(Basis.from_scale(Vector3.ONE * 1.2), Vector3(sp.x, w.height_at(sp) + 0.012, sp.y)), Color(1, 1, 1, 0.5))
	# the camp behind the engines
	for j in 4:
		var a2 := angle + (j - 1.5) * 0.28
		var tp := c + Vector2(cos(a2), sin(a2)) * (ring + 1.35)
		var ttq := w.tile_at(tp)
		if ttq < 0 or not w.is_land(ttq):
			continue
		props.add("hide_tent", _xf(tp, w.height_at(tp), -a2 + PI * 0.5, 1.2), Color(tint.r, tint.g, tint.b, M7WorldData.hash01(j, 3, 82)))
	var bp := c + Vector2(cos(angle), sin(angle)) * (ring + 1.1)
	props.add("banner%d" % civ, _xf(bp, w.height_at(bp), -0.4, 1.3), Color(tint.r, tint.g, tint.b, seed))

const WALL_RING := 0.26        # from the outermost house to the stone wall ring
const WALL_CLEAR := 0.45       # props that stood just outside the houses stand this much farther out behind a stone wall
const WALL_SCALE := 1.2        # the wall pieces are drawn larger than life, like the rest of the village
const CASTLE_GAP_WALLED := 0.66   # from the stone wall ring to the castle's middle
const CASTLE_GAP_OPEN := 0.78     # from the outermost house to the castle's middle when the village has no wall
const CASTLE_SCALE := 1.25

## The stone wall ring of a village (DEF-05): runs of curtain wall round it with towers at intervals, a gate facing a seed-chosen way,
## and, with a gatehouse (DEF-06), two roofed towers and a bridge room over the gate. A bigger settlement has a higher wall and more towers
## (`rings` is the sim's count: 1 village, 2 town, 3 city). Segments stop at water and at rivers, as the palisade does.
func _stone_walls(t: int, c: Vector2, seed: float, ring: float, rings: int, gatehouse: bool, gate_pref: float, siege: Dictionary = {}) -> Vector2:
	var n := int(ceil(TAU * ring / (ArtDefense.SEG * WALL_SCALE * 0.97)))
	var chord := 2.0 * ring * sin(PI / n) * 1.04
	var sy := 1.0 + 0.2 * (rings - 1)
	# the gate faces the preferred way unless that is water or a riverbank: then the nearest way (30 degrees at a time) that is not
	var gate_a := gate_pref
	for k in 12:
		var ga := gate_pref + TAU / 12.0 * (((k + 1) / 2) * (1 if k % 2 == 1 else -1))
		var gq := c + Vector2(cos(ga), sin(ga)) * ring
		var gt := w.tile_at(gq)
		if gt >= 0 and w.is_land(gt) and _river_dist(gq) >= 0.14:
			gate_a = ga
			break
	var tower_every := maxi(3, n / (6 + 2 * rings))
	# a siege (WAR-09) wears the wall down on the side the engines stand: the lower the integrity, the more runs are broken, then heaped
	var integrity := int(siege.get("integrity", 1000)) if not siege.is_empty() else 1000
	var broken := roundi((1.0 - integrity / 1000.0) * 7.0)
	var breach_i := roundi(fposmod(float(siege.get("angle", 0.0)) - gate_a, TAU) / TAU * n) % n if not siege.is_empty() else 0
	for i in n:
		var a := gate_a + TAU * i / n
		var q := c + Vector2(cos(a), sin(a)) * ring
		var tq := w.tile_at(q)
		var tang := a + PI * 0.5
		if i == 0:
			# the gate, in the middle of the first slot, its front toward the village
			var mesh_name := "wall_gatehouse" if gatehouse else "wall_gate"
			var gb := Basis(Vector3.UP, -tang) * Basis.from_scale(Vector3(WALL_SCALE, WALL_SCALE * sy, WALL_SCALE))
			props.add(mesh_name, Transform3D(gb, Vector3(q.x, w.height_at(q), q.y)), Color(1, 1, 1, seed))
			continue
		if tq < 0 or not w.is_land(tq) or _river_dist(q) < 0.08:
			continue
		var segb := Basis(Vector3.UP, -tang) * Basis.from_scale(Vector3(chord / ArtDefense.SEG, WALL_SCALE * sy, WALL_SCALE))
		var off_breach := mini(absi(i - breach_i), n - absi(i - breach_i))
		var seg_mesh := "wall_seg"
		if broken > 0 and off_breach * 2 <= broken:
			seg_mesh = "wall_rubble" if (integrity <= 150 and off_breach <= 1) else "wall_ruin"
		props.add(seg_mesh, Transform3D(segb, Vector3(q.x, w.height_at(q), q.y)), Color(1, 1, 1, seed))
		# a tower where this run meets the next (a tower beside a breach comes down with it)
		if i % tower_every == 0 and not (broken > 3 and off_breach <= 2):
			var ab := a + TAU * 0.5 / n
			var tp := c + Vector2(cos(ab), sin(ab)) * ring
			var ttq := w.tile_at(tp)
			if ttq >= 0 and w.is_land(ttq) and _river_dist(tp) >= 0.08:
				var tb := Basis(Vector3.UP, -(ab + PI * 0.5)) * Basis.from_scale(Vector3(WALL_SCALE, WALL_SCALE * (1.0 + 0.15 * (rings - 1)), WALL_SCALE))
				props.add("wall_tower", Transform3D(tb, Vector3(tp.x, w.height_at(tp), tp.y)), Color(1, 1, 1, seed))
	return c + Vector2(cos(gate_a), sin(gate_a)) * ring

## The castle stage a settlement shows: 4 built, 1 to 3 while one is going up (a third of the labor each), 0 none.
func castle_stage(s: Dictionary) -> int:
	if w.iron_works(s, "castle") > 0:
		return 4
	var k := w.iron_work_ids.find("castle")
	if k >= 0 and int(s.get("ironBuildKind", -1)) == k:
		var pm := int(s.get("ironBuildPermille", 0))
		return 0 if pm <= 0 else 1 + mini(2, pm * 3 / 1000)
	return 0

## Ground a castle may stand on: dry land that is not a field, a mountain or a riverbank, within a modest climb of the village's own level.
func _castle_ground_ok(p: Vector2, h0: float) -> bool:
	var t := w.tile_at(p)
	if t < 0 or not w.is_land(t) or absf(w.heights[t] - h0) > 0.35:
		return false
	return w.plot_kind[t] < 0 and w.biome[t] < M7WorldData.Biome.MOUNTAIN and _river_dist(p) >= 0.11

## Where a castle stands: on the highest level, clear ground in a ring round the village (a castle holds ground), clear of what is placed.
func _castle_site(c: Vector2, h0: float, seed: float, radius: float, placed: Array, gate_at: Vector2) -> Variant:
	var half := Vector2(0.28, 0.27) * CASTLE_SCALE
	var best: Variant = null
	var best_score := -1e9
	for ring_k in 3:
		for k in 24:
			var a := seed * TAU + TAU * k / 24.0
			var p := c + Vector2(cos(a), sin(a)) * (radius + 0.12 * ring_k)
			if not _castle_ground_ok(p, h0):
				continue
			var yaw := atan2((c - p).x, (c - p).y)
			var y := w.height_at(p)
			var ok := true
			for lc in [Vector2(-half.x, -half.y), Vector2(half.x, -half.y), Vector2(half.x, half.y), Vector2(-half.x, half.y)]:
				var q3: Vector3 = rot_y(yaw) * Vector3(lc.x, 0, lc.y)
				var q := p + Vector2(q3.x, q3.z)
				if not _castle_ground_ok(q, h0):
					ok = false
					break
				y = minf(y, w.height_at(q))
			if not ok:
				continue
			for pp in placed:
				if (pp as Vector2).distance_to(p) < 0.4:
					ok = false
					break
			if ok and gate_at.distance_to(p) < 0.95:
				ok = false        # the way into the village stays clear
			if ok:
				# a ziggurat or temple keeps its ground
				for zs in ziggurat_sites:
					if (zs.p as Vector2).distance_to(p) < 0.85:
						ok = false
						break
				for cs in civic_sites:
					if (cs.p as Vector2).distance_to(p) < 0.7:
						ok = false
						break
			if ok:
				for tp in tree_points:
					if (tp as Vector2).distance_squared_to(p) < 0.12:
						ok = false
						break
			if not ok:
				continue
			var score := y * 4.0 - ring_k * 0.3 + M7WorldData.hash01(int(a * 100.0), ring_k, 77) * 0.05
			if score > best_score:
				best_score = score
				best = {"p": p, "y": y, "yaw": yaw}
		if best != null:
			break
	return best

## A castle (DEF-08), or the stages of one being raised, beside the village; the owner's banner flies from the keep and, when soldiers
## are posted, guards stand on its walls.
func _castle(s: Dictionary, t: int, c: Vector2, h0: float, seed: float, radius: float, placed: Array, cc: Color, gate_at: Vector2) -> void:
	var stage := castle_stage(s)
	if stage <= 0:
		return
	var site = _castle_site(c, h0, seed, radius, placed, gate_at)
	if site == null:
		return
	var p: Vector2 = site.p
	var yaw: float = site.yaw
	var basis := Basis(Vector3.UP, yaw) * Basis.from_scale(Vector3.ONE * CASTLE_SCALE)
	props.add("castle_%d" % stage, Transform3D(basis, Vector3(p.x, site.y, p.y)), Color(cc.r, cc.g, cc.b, seed))
	placed.append(p)
	castle_sites.append({"p": p, "y": site.y, "yaw": yaw, "stage": stage, "civ": int(s.civ), "tile": t})
	var guards := mini(int(s.get("soldiers", 0)), 6) if stage == 4 else 0
	var civ := int(s.civ)
	for gi in guards:
		var ang := TAU * gi / 6.0 + 0.4
		var local := Vector3(cos(ang), 0.0, sin(ang)) * 0.15 * CASTLE_SCALE
		var gp: Vector3 = rot_y(yaw) * local
		var wp := p + Vector2(gp.x, gp.z)
		var gb := Basis(Vector3.UP, yaw + ang + PI * 0.5) * Basis.from_scale(Vector3.ONE * 1.1)
		props.add("person_soldier", Transform3D(gb, Vector3(wp.x, site.y + 0.02 * CASTLE_SCALE, wp.y)), Color(float(civ) + 0.5 * 0.98, 0.0, 0.0, 0.0))

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

const HARBOR_SCALE := 1.15
const HARBOR_MOOR := 0.82      # from the hearth tile's centre to where boats lie between a harbor's piers

## A harbor (SEA-07): a quay on the shore of the first water neighbour (the one `_moor_fleet` picks) and two piers out over the water. True when
## it stands (a village on a river with no sea beside it has none).
func _harbor(s: Dictionary, t: int, c0: Vector2, seed: float, cc: Color) -> bool:
	for i in 6:
		var nb: int = w.neighbour(t, (i + int(seed * 6.0)) % 6)
		if nb >= 0 and w.is_water(nb):
			var dir: Vector2 = (w.tile_center(nb) - c0).normalized()
			var at := c0 + dir * 0.5
			var yaw := atan2(-dir.x, -dir.y)
			props.add("harbor", Transform3D(Basis(Vector3.UP, yaw) * Basis.from_scale(Vector3.ONE * HARBOR_SCALE), Vector3(at.x, 0.0, at.y)),
				Color(cc.r, cc.g, cc.b, seed))
			harbor_sites.append({"p": at, "y": 0.0})
			# a war galley (SEA-09) lies between the piers off the end of the quay; a blockade (a rival's galleys) holds the harbor mouth
			var tangent := dir.rotated(PI * 0.5)
			var tint := M7MeshBuilder.lin(cc, 0.5)
			if w.iron_works(s, "galley") > 0:
				var gp := c0 + dir * 1.18
				props.add("galley", Transform3D(Basis(Vector3.UP, atan2(tangent.x, tangent.y) + PI * 0.0) * Basis.from_scale(Vector3.ONE * 1.1), Vector3(gp.x, -0.02, gp.y)),
					Color(cc.r, cc.g, cc.b, seed))
			var enemy := w.blockaders_of(int(s.get("index", -1)))
			for k in enemy.size():
				var civ_b := int(enemy[k])
				var et := M7MeshBuilder.lin(M7WorldData.civ_color(civ_b), 0.5)
				for j in 2:
					var bp := c0 + dir * 2.2 + tangent * (j - 0.5) * 0.7
					var bt := w.tile_at(bp)
					if bt >= 0 and w.is_water(bt):
						props.add("galley", Transform3D(Basis(Vector3.UP, atan2(tangent.x, tangent.y) + PI * 0.15 * (1.0 if j == 0 else -1.0)) * Basis.from_scale(Vector3.ONE * 1.1),
							Vector3(bp.x, -0.02, bp.y)), Color(et.r, et.g, et.b, 0.5))
			return true
	return false

## The stone bridges (TRN-08) where a bridged paved road crosses a river: one per run of river tiles on a road, over the nearest river reach,
## turned to cross the river squarely.
func _bridges() -> void:
	var done := {}
	for ri in range(0, w.road_meta.size(), 4):
		if w.road_meta[ri + 3] == 0:
			continue
		var start := w.road_meta[ri + 1]
		var length := w.road_meta[ri + 2]
		var j := 1
		while j < length - 1:
			var t := w.road_tiles[start + j]
			if t < 0 or (w.flags[t] & M7WorldData.FLAG_RIVER) == 0:
				j += 1
				continue
			var j0 := j
			while j < length - 1 and w.road_tiles[start + j] >= 0 and (w.flags[w.road_tiles[start + j]] & M7WorldData.FLAG_RIVER) != 0:
				j += 1
			var jm := (j0 + j - 1) / 2
			var tm := w.road_tiles[start + jm]
			var tp := w.road_tiles[start + jm - 1]
			var tn := w.road_tiles[start + jm + 1]
			if tm < 0 or tp < 0 or tn < 0 or done.has(tm):
				continue
			done[tm] = true
			var d: Vector2 = (w.tile_center(tn) - w.tile_center(tp)).normalized()
			var p := w.tile_center(tm)
			var river := _river_near(p)
			if float(river[0]) > 0.6:
				continue
			p = river[1]
			var rdir: Vector2 = river[2]
			if rdir != Vector2.ZERO and absf(d.cross(rdir)) < 0.7:
				var perp := rdir.rotated(PI * 0.5)
				d = perp if perp.dot(d) >= 0.0 else -perp
			var yaw := atan2(-d.y, d.x)
			var y := maxf(w.height_at(p), 0.0)
			props.add("bridge", Transform3D(Basis(Vector3.UP, yaw) * Basis.from_scale(Vector3.ONE * 1.0), Vector3(p.x, y, p.y)), Color(1, 1, 1, 0.5))
			bridge_sites.append({"p": p, "y": y})

## The boats a village keeps: one to four (by population) moored along the nearest shore or river bank, of the best kind its
## civilization knows (1 raft .. 4 outrigger). Nothing happens if there is no water within a short walk.
func _moor_fleet(t: int, c0: Vector2, seed: float, tier: int, population: int, cc: Color, quay: bool = false) -> void:
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
			anchor = c0 + dir * (HARBOR_MOOR if quay else 0.6)
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
		var off := (k - (count - 1) * 0.5) * (0.2 if tier < 5 else 0.36)
		var q := anchor + along * off
		var tq := w.tile_at(q)
		if tq < 0:
			continue
		var y := (maxf(w.height_at(q), 0.0) + 0.012) if level < 0.0 else level
		var hv := M7WorldData.hash01(t, k, 31)
		var yaw := atan2(along.x, along.y) + (hv - 0.5) * 0.35
		props.add(mesh_name, _xf(q, y, yaw, 1.0), Color(cc.r, cc.g, cc.b, hv))

func _camps() -> void:
	for ci in w.camps.size():
		var cp: Array = w.camps[ci]
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
		if _camp_fire_lit(ci):
			props.add("flame", _xf(c + Vector2(0.03, 0.12), h + 0.003, seed, 0.6), Color(1, 1, 1, seed))
		props.add("banner_small%d" % civ, _xf(c + Vector2(-0.07, -0.02), h, -0.4, 1.0), Color(cc.r, cc.g, cc.b, seed))

# ------------------------------------------------------------------ deposits

## A marker on every identified deposit and generic geological clue (red channel: DepositKind + 1, or 23 for a clue).
## It stands to one side of the tile's middle, clear of a village's hearth, and keeps the same spot for the life of the match.
const DEPOSIT_SCALE := 1.3
const MINE_AWAY_FROM_CAMP := PI + 0.58   # opposite the centroid of a camp's props, about (0.14, 0.09) from the tile centre

## Byte 1 of a tile's e3 overlay entry is a bitfield (Game.cs `BuildE3OverlayBytes`).
const MINE_WORKED := 1      # the deposit has produced ore
const MINE_REACH := 2       # within a settlement's mining reach (terrain overlay only)
const MINE_DEEP := 4        # a known deposit of depth >= 2 (needs a shaft)
const MINE_EXHAUSTED := 8   # no ore left; only ever set together with MINE_WORKED

## The mesh a deposit of `kind` shows for its byte-1 bits `g` (art_deposits.gd / art_mines.gd), `camp_head` when the mining camp on the
## tile has built its mine head. Untouched: the marker. Worked: copper ore, arsenical copper and iron show the adit, or the headframe
## when the deposit is deep or the camp has its mine head; tin shows the stream working, or the headframe only when it is a deep lode
## (a shaft over a placer makes no sense); gold and silver are always panned; salt is always the salt works. Exhausted: the same family's
## `_spent` mesh. Without the `_tall` suffix that mountain tiles add.
static func mine_mesh_name(kind: int, g: int, camp_head: bool) -> String:
	if (g & MINE_WORKED) == 0 or kind not in M7_MINED_KINDS:
		return "deposit%d" % kind
	var deep := (g & MINE_DEEP) != 0
	var head := false
	if kind in [6, 7, 11]:
		head = deep or camp_head
	elif kind == 8:
		head = deep
	var name := "deposit_mine%d%s" % [kind, "_head" if head else ""]
	if (g & MINE_EXHAUSTED) != 0:
		name += "_spent"
	return name

const M7_MINED_KINDS := [6, 7, 8, 9, 10, 11, 12]

## A known deposit shows its marker; once it has produced ore its working replaces the marker on the same spot and turn (or, on a tile
## with a mining camp, on the far side of the tile from the camp), see `mine_mesh_name`. A working's pennant takes the colour of the
## camp's civilization, else the tile owner's, else plain hide brown; a spent working has no pennant.
func _deposits() -> void:
	var bytes := w.e3_overlay_bytes
	var tiles := w.width * w.height
	if bytes.size() != tiles * 4:
		return
	var camps := _mining_camps_by_tile()
	var gallery := _mine_gallery(bytes) if MINE_GALLERY else {}
	for t in tiles:
		var kind := int(bytes[t * 4]) - 1
		var g := int(bytes[t * 4 + 1])
		var camp_head := camps.has(t) and int(camps[t][1]) != 0
		if MINE_DEBUG and kind > 0:
			g |= MINE_WORKED
			if M7WorldData.hash01(t, 37) < 0.2:
				g |= MINE_EXHAUSTED
			if not camps.has(t):
				camp_head = M7WorldData.hash01(t, 29) < 0.5
		var a := M7WorldData.hash01(t, 17) * TAU
		var p := w.tile_center(t) + Vector2(cos(a), sin(a)) * 0.2
		var yaw := M7WorldData.hash01(t, 19) * TAU
		if gallery.has(t):
			kind = int(gallery[t][0])
			g = int(gallery[t][1])
			camp_head = bool(gallery[t][2])
			p = w.tile_center(t)
			yaw = 0.0
		if kind <= 0 or not w.is_land(t) or not kit.meshes.has("deposit%d" % kind):
			continue
		var tall := "_tall" if w.biome[t] >= M7WorldData.Biome.MOUNTAIN else ""
		var seed := M7WorldData.hash01(t, 23)
		var mesh := mine_mesh_name(kind, g, camp_head)
		if not kit.meshes.has(mesh + tall):
			mesh = "deposit%d" % kind
		if mesh.begins_with("deposit_mine"):
			if camps.has(t) and not gallery.has(t):
				# the camp's tents, hearth and banner stand at +(0.12, 0.05) (`_camps`): dig on the far side of the tile from them
				a = MINE_AWAY_FROM_CAMP + (M7WorldData.hash01(t, 17) - 0.5) * 1.1
				p = w.tile_center(t) + Vector2(cos(a), sin(a)) * 0.22
			var civ: int = int(camps[t][0]) if camps.has(t) else (w.owner[t] if t < w.owner.size() else -1)
			var cc := M7MeshBuilder.lin(M7WorldData.civ_color(civ) if civ >= 0 else Color("#9c8466"), 0.5)
			props.add(mesh + tall, _xf(p, w.height_at(p), yaw, DEPOSIT_SCALE), Color(cc.r, cc.g, cc.b, seed))
			continue
		props.add(mesh + tall, _xf(p, w.height_at(p), yaw, DEPOSIT_SCALE), Color(1, 1, 1, seed))

## What the deposit props depend on: the e3 overlay (kind, worked bit) and each mining camp's tile, civilization and mine head.
func _deposit_digest() -> int:
	var h := hash(w.e3_overlay_bytes)
	for cd in w.camp_details:
		if int(cd[2]) == 1:
			h = int((h * 31 + int(cd[0]) * 17 + int(cd[1]) * 3 + int(cd[7])) & 0x7fffffff)
	return h

## tile -> [civ, mine head 0/1] for every mining camp in the frame (camp_details kind 1; the camp stands on the deposit it digs).
func _mining_camps_by_tile() -> Dictionary:
	var out := {}
	for cd in w.camp_details:
		if int(cd[2]) == 1:
			out[int(cd[0])] = [int(cd[1]), int(cd[7])]
	return out

## `--mine-debug` draws every known deposit of the mined kinds as worked, about a fifth of them exhausted, and on tiles without a camp
## gives about half a camp mine head. `--mine-gallery` also relabels five rows of eight tiles around the busiest known deposit cluster
## (the tile the `deposits` scenario centres on), columns copper ore, arsenical copper, iron, tin placer, tin lode (deep), gold, silver,
## salt; rows north to south: untouched marker, working, working with a camp mine head, exhausted, exhausted with a camp mine head. All
## drawn at the tile centre facing south. Renderer only: the simulation never sees either.
var MINE_GALLERY: bool = "--mine-gallery" in OS.get_cmdline_user_args()
var MINE_DEBUG: bool = MINE_GALLERY or "--mine-debug" in OS.get_cmdline_user_args()
const MINE_GALLERY_COLUMNS := [[6, 0], [7, 0], [11, 0], [8, 0], [8, MINE_DEEP], [9, 0], [10, 0], [12, 0]]
const MINE_GALLERY_ROWS := [[0, false], [MINE_WORKED, false], [MINE_WORKED, true], [MINE_WORKED | MINE_EXHAUSTED, false],
	[MINE_WORKED | MINE_EXHAUSTED, true]]

func _mine_gallery(bytes: PackedByteArray) -> Dictionary:
	var best := -1
	var best_count := 0
	for t in w.width * w.height:
		if bytes[t * 4] == 0:
			continue
		var count := 0
		var x0 := t % w.width
		var y0 := t / w.width
		for dy in range(-3, 4):
			for dx in range(-3, 4):
				var x := x0 + dx
				var y := y0 + dy
				if x >= 0 and y >= 0 and x < w.width and y < w.height and bytes[(y * w.width + x) * 4] != 0:
					count += 1
		if count > best_count:
			best_count = count
			best = t
	var out := {}
	if best < 0:
		return out
	var cx := best % w.width
	var cy := best / w.width
	for row in MINE_GALLERY_ROWS.size():
		var y := cy - 4 + row * 2   # every other row, so the hex stagger keeps the columns straight
		for i in MINE_GALLERY_COLUMNS.size():
			var x := cx - 4 + i
			if x < 0 or y < 0 or x >= w.width or y >= w.height:
				continue
			var col: Array = MINE_GALLERY_COLUMNS[i]
			out[y * w.width + x] = [col[0], int(MINE_GALLERY_ROWS[row][0]) | int(col[1]), MINE_GALLERY_ROWS[row][1]]
	return out

# ------------------------------------------------------------------ people

## `--boat-debug` puts every adult near water in a boat, tier 1 to 4 by figure index.
var BOAT_DEBUG: bool = "--boat-debug" in OS.get_cmdline_user_args()

func _debug_boat_tier(fi: int) -> int:
	return 1 + (fi / 11) % 6

## `--tier-debug` gives every adult a tool and cycles the tier by figure index so all four looks can be seen at once.
var TIER_DEBUG: bool = "--tier-debug" in OS.get_cmdline_user_args()

## The farm ladder's rung for the iron scythe (AGR-09; ard plough is 3).
const IRON_SCYTHE_RUNG := 4

const TOOL_BY_ACTIVITY := {
	0: "plain", 1: "basket", 2: "spear", 3: "pole", 4: "hoe", 5: "axe", 6: "basket", 7: "hammer", 8: "hammer",
	9: "plain", 10: "plain", 11: "plain", 12: "plain", 15: "bundle", 16: "bundle", 17: "spear", 18: "spear", 19: "spear", 22: "spear",
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
		# raiders of a civilization with chariots (WAR-06) ride them over open ground
		if boat_strength <= 0.0 and age == 1 and civ >= 0 and w.has_look(civ, M7WorldData.LOOK_CHARIOTS):
			var raiding := act == 17 or act == 18 or act == 19
			var on_wheels := (raiding and seed < 0.7) or (M7WorldData.LOOKS_DEBUG and (fi / 11) % 5 == 0)
			var here_tile: int = w.tile_at(p)
			if on_wheels and here_tile >= 0 and _open_ground(here_tile):
				props.add("person_chariot", _xf(p, y, face, 1.0), custom)
				props.add("shadow_disc", _xf(p, y + 0.001, 0.0, 2.4), Color(offset.x, dy, offset.y, float(mode_offset / 16)))
				chariot_probe = p
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
			if act == 12 and civ >= 0 and w.has_look(civ, M7WorldData.LOOK_HERD_FOLLOWING):
				mesh = "person_crook"     # herders walk with a crook once they follow the herds (HUS-02)
			var iron_armed := false
			if fl & 256:
				mesh = "person_spear"
				if civ >= 0 and w.has_look(civ, M7WorldData.LOOK_IRON_ARMS):
					mesh = "person_soldier"       # iron arms (WAR-08): helmet, shield and an iron spear
					iron_armed = true
			# bladed and headed tools show the tier of the method in use: stone, polished stone, copper, bronze
			var tier := M7MeshKit.tool_tier_for_rung(rung) if rung >= 0 else 0
			# the iron kits (11, 12) show on the figure that holds one: dark iron, then bright tempered steel (MET-09, MET-10)
			var kit_tier := (fl >> 20) & 15
			if kit_tier >= 4:
				tier = kit_tier
			# AGR-09 Iron Sickle & Scythe: a farmer on the scythe rung of the farm ladder swings one
			if act == 4 and rung >= IRON_SCYTHE_RUNG and not (fl & 256):
				mesh = "person_scythe"
				tier = maxi(tier, 4)
			if TIER_DEBUG:
				var idx := fi / 11
				mesh = ["person_spear", "person_axe", "person_hoe", "person_hammer", "person_scythe"][idx % 5]
				tier = (idx / 3) % M7MeshKit.TOOL_TIERS
				if mesh == "person_scythe":
					tier = maxi(tier, 4)
			if tier > 0 and not iron_armed and mesh in ["person_spear", "person_axe", "person_hoe", "person_hammer", "person_scythe"]:
				mesh += "_t" + str(tier)
		if civ >= 0 and act != 14 and w.has_look(civ, M7WorldData.LOOK_TAILORED):
			mesh += "_g"                   # fitted parkas, sleeves, leggings and boots (FIB-04)
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

## Ground a chariot can run on: no forest, wetland or mountain.
func _open_ground(t: int) -> bool:
	var B := M7WorldData.Biome
	var b := int(w.biome[t])
	return b == B.TUNDRA or b == B.GRASSLAND or b == B.SAVANNA or b == B.OPEN_WOODLAND or b == B.DESERT

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
		_herders(p, pp, moved, heading, cnt, id, pmx < 0)
	stats.animals = n

## Herd following (HUS-02): one or two herders walk beside a wild herd on the land of a civilization that knows it, one on each
## flank, moving with it. They glide like any other figure.
func _herders(p: Vector2, pp: Vector2, moved: bool, heading: float, animals: int, id: int, fresh: bool) -> void:
	var here: int = w.tile_at(p)
	var keeper: int = w.owner[here] if here >= 0 else -1
	if keeper < 0 or animals < 3 or not w.has_look(keeper, M7WorldData.LOOK_HERD_FOLLOWING):
		return
	var travel := Vector2(sin(heading), cos(heading))
	var side := Vector2(cos(heading), -sin(heading))
	var mesh := "person_crook_g" if w.has_look(keeper, M7WorldData.LOOK_TAILORED) else "person_crook"
	for i in (2 if animals >= 5 else 1):
		var q := p + side * (0.11 if i == 0 else -0.12) - travel * (0.0 if i == 0 else 0.06)
		var tile := w.tile_at(q)
		if tile < 0 or not w.is_land(tile):
			continue
		var hv := M7WorldData.hash01(id + i, 0, 3)
		var offset := Vector2.ZERO
		var dy := 0.0
		if moved:
			offset = pp - p
			dy = maxf(w.height_at(q + offset), 0.0) - w.heights[tile]
		var custom := _glide_custom(keeper % 8, 16 if fresh else 0, hv, dy, offset)
		props.add(mesh, _xf(q, w.heights[tile], heading, 1.0), custom)
		props.add("shadow_disc", _xf(q, w.heights[tile] + 0.001, 0.0, 1.0), Color(offset.x, dy, offset.y, float(16 / 16 if fresh else 0)))

func _beacons() -> void:
	for beacon in w.beacons:
		var t := int(beacon[0])
		if not w.is_land(t) or (t < w.visibility.size() and w.visibility[t] == 0):
			continue
		var c := w.tile_center(t)
		var cc := M7WorldData.civ_color(int(beacon[1]))
		props.add("beacon", _xf(c, w.heights[t], float(t) * 0.03, 1.25), Color(cc.r, cc.g, cc.b, 0.5))
