## The production low-poly renderer. C# owns the simulation and supplies immutable
## viewer-filtered MatchFrame snapshots; this script only builds and updates visuals.
extends Node3D

const M7WorldData = preload("res://Renderer/LowPoly3D/Scripts/world_data.gd")
const M7MeshKit = preload("res://Renderer/LowPoly3D/Scripts/mesh_kit.gd")
const M7WorldBuilder = preload("res://Renderer/LowPoly3D/Scripts/world_builder.gd")
const M7ZigguratRig = preload("res://Renderer/LowPoly3D/Scripts/zig_rig.gd")
const M7SkirmishRig = preload("res://Renderer/LowPoly3D/Scripts/skirmish_rig.gd")

var world: M7WorldData
var kit: M7MeshKit
var mats := {}
var builder: M7WorldBuilder
var world_root: Node3D
var camera: Camera3D
var sun: DirectionalLight3D
var environment: Environment
var camera_attributes: CameraAttributesPractical
var state_image: Image
var state_texture: ImageTexture
var overlay_image: Image
var overlay_texture: ImageTexture
var e3_overlay_image: Image
var e3_overlay_texture: ImageTexture
var forest_overlay_image: Image
var forest_overlay_texture: ImageTexture
var overlay_mode := 0 # 0 none, 1 claims, 2 food, 3 methods, 4 deposits, 5 trade, 6 cohesion, 7 forest
var hearth_fx_by_tile := {}
var last_season := -1
var season_lock := -1                  # dev aid for scenarios: show this season whatever the date (-1 follows the frame)
var signal_rig: M7SignalRig
var burn_rig: M7BurnRig
var feast_rig: M7FeastRig
var dog_rig: M7DogRig
var farm_rig: M7FarmRig
var skirmish_rig: M7SkirmishRig
var zig_rig: M7ZigguratRig
var golden_hour := false
var camera_focus := Vector3.ZERO
var camera_distance := 40.0
var regional_render_scale_active := false
var _render_tick_profile_max_usec := 0
var _render_tick_profile_parts: Dictionary = {}

func setup_frame(frame_data: Dictionary) -> void:
	M7BurnRig.install_empty()
	world = M7WorldData.new()
	if not world.load_frame_data(frame_data, true):
		push_error("Could not read initial MatchFrame")
		return
	kit = M7MeshKit.new()
	_make_state_texture()
	_make_overlay_texture()
	_make_materials()
	_make_environment()
	_make_camera()
	_make_grade_overlay()
	RenderingServer.global_shader_parameter_set("map_lat", Vector2(world.height * M7WorldData.S * 0.5, world.height * M7WorldData.S * 0.5))
	builder = M7WorldBuilder.new(world, kit, mats)
	world_root = builder.build()
	add_child(world_root)
	signal_rig = M7SignalRig.new(builder, world_root, Callable(self, "_make_smoke"))
	signal_rig.sync()
	burn_rig = M7BurnRig.new(builder, world_root, Callable(self, "_make_smoke"))
	burn_rig.sync(float(world.tick))
	burn_rig.set_corridors(builder.trail_polys)
	builder.trails_changed = false
	feast_rig = M7FeastRig.new(builder, world_root)
	feast_rig.sync(float(world.tick))
	dog_rig = M7DogRig.new(builder, world_root)
	dog_rig.sync()
	farm_rig = M7FarmRig.new(builder, world_root)
	farm_rig.sync()
	skirmish_rig = M7SkirmishRig.new(builder, world_root)
	zig_rig = M7ZigguratRig.new(builder, world_root, Callable(self, "_make_smoke"))
	zig_rig.sync()
	_apply_season(world.season)
	_set_villages()
	_sync_hearth_fx()
	print("M7a low-poly world built: ", builder.stats)

func update_frame(frame_data: Dictionary) -> void:
	var started := Time.get_ticks_usec()
	if world == null or builder == null or not world.load_frame_data(frame_data):
		return
	var loaded := Time.get_ticks_usec()
	_update_state_texture()
	_update_overlay_texture()
	_update_e3_overlay_texture()
	_update_forest_overlay_texture()
	var tile_state := Time.get_ticks_usec()
	_apply_season(season_lock if season_lock >= 0 else world.season)
	if mats.has("terrain"):
		mats.terrain.set_shader_parameter("overlay_forecast", 1.0 if world.season >= 2 else 0.0)
	var structures_changed := builder.update_frame()
	if builder.trails_changed:
		builder.trails_changed = false
		burn_rig.set_corridors(builder.trail_polys)
	signal_rig.sync()
	dog_rig.sync()
	farm_rig.sync()
	zig_rig.sync()
	var built := Time.get_ticks_usec()
	_set_villages()
	if structures_changed:
		_sync_hearth_fx()
	if "--perf-debug" in OS.get_cmdline_user_args():
		print("M7A_FRAME_GDSCRIPT load=", (loaded - started) / 1000.0,
			" tile_state=", (tile_state - loaded) / 1000.0,
			" scene=", (built - tile_state) / 1000.0,
			" rest=", (Time.get_ticks_usec() - built) / 1000.0)

## The host's smooth simulation tick, every frame: the megalith's raising is played from it.
func set_render_tick(tick: float, profile: bool = false) -> void:
	var profile_started := Time.get_ticks_usec() if profile else 0
	var phase_started := profile_started
	var parts: Dictionary = {}
	if builder != null and builder.rig != null:
		builder.rig.update(tick)
		if profile:
			parts["monument"] = Time.get_ticks_usec() - phase_started
			phase_started = Time.get_ticks_usec()
	if burn_rig != null:
		burn_rig.sync(tick)
		if profile:
			parts["burn_sync"] = Time.get_ticks_usec() - phase_started
			phase_started = Time.get_ticks_usec()
		burn_rig.update(tick)
		if profile:
			parts["burn_update"] = Time.get_ticks_usec() - phase_started
			phase_started = Time.get_ticks_usec()
	if feast_rig != null:
		feast_rig.sync(tick)
		if profile:
			parts["feast_sync"] = Time.get_ticks_usec() - phase_started
			phase_started = Time.get_ticks_usec()
		feast_rig.update(tick)
		if profile:
			parts["feast_update"] = Time.get_ticks_usec() - phase_started
			phase_started = Time.get_ticks_usec()
	if dog_rig != null:
		dog_rig.update(tick)
		if profile:
			parts["dog_update"] = Time.get_ticks_usec() - phase_started
			phase_started = Time.get_ticks_usec()
	if farm_rig != null:
		farm_rig.update(tick)
		if profile:
			parts["farm_update"] = Time.get_ticks_usec() - phase_started
	if zig_rig != null:
		zig_rig.update(tick)
	if profile:
		var total_usec := Time.get_ticks_usec() - profile_started
		if total_usec > _render_tick_profile_max_usec:
			_render_tick_profile_max_usec = total_usec
			_render_tick_profile_parts = parts

func reset_render_tick_profile() -> void:
	_render_tick_profile_max_usec = 0
	_render_tick_profile_parts.clear()

func get_render_tick_profile() -> String:
	return "tick_us=%d parts=%s" % [_render_tick_profile_max_usec, _render_tick_profile_parts]

## Dev aid for scenarios: show one season (0 spring .. 3 winter) whatever the date; -1 lets the frame decide again.
func lock_season(season: int) -> void:
	season_lock = season
	if season >= 0:
		_apply_season(season)

## Dev aid for scenarios: the first monument's ring centre in world coordinates (or the origin when there is none).
func monument_focus() -> Vector3:
	if builder == null or builder.rig == null or builder.rig.sites.is_empty():
		return Vector3.ZERO
	var c: Vector2 = builder.rig.sites[0].center
	return Vector3(c.x, maxf(world.height_at(c), 0.0), c.y)

## Dev aid for scenarios: the middle of the first trail, or the origin when there is none.
func trail_focus() -> Vector3:
	if world == null or world.trail_meta.size() < 5:
		return Vector3.ZERO
	var start := world.trail_meta[3]
	var length := world.trail_meta[4]
	var tile := world.trail_tiles[start + length / 2]
	if tile < 0:
		return Vector3.ZERO
	var c: Vector2 = world.tile_center(tile)
	return Vector3(c.x, maxf(world.height_at(c), 0.0), c.y)

## Dev aid for scenarios: where the last boat was placed (the origin when none).
func boat_focus() -> Vector3:
	if builder == null or builder.boat_probe == Vector2.ZERO:
		return Vector3.ZERO
	var c: Vector2 = builder.boat_probe
	return Vector3(c.x, maxf(world.height_at(c), 0.0), c.y)

## Dev aids for scenarios: how many signal posts stand, and where the first one is (the origin when none).
func signal_count() -> int:
	return signal_rig.posts.size() if signal_rig != null else 0

func signal_focus() -> Vector3:
	if signal_rig == null or signal_rig.posts.is_empty():
		return Vector3.ZERO
	var p: Vector2 = signal_rig.posts[0].pos
	return Vector3(p.x, maxf(world.height_at(p), 0.0), p.y)

## Dev aid for scenarios: where a fire is burning now (the origin when none).
func burn_focus() -> Vector3:
	if burn_rig == null or not burn_rig.fx_active:
		return Vector3.ZERO
	var c: Vector2 = burn_rig.fx_focus
	return Vector3(c.x, maxf(world.height_at(c), 0.0), c.y)

## Dev aids for scenarios: where a village dog is, and the middle of the first track a dog hauls along (the origin when none).
func dog_count() -> Vector2i:
	return Vector2i(dog_rig.dogs.size(), dog_rig.haulers.size()) if dog_rig != null else Vector2i.ZERO

func dog_focus() -> Vector3:
	return dog_rig.focus() if dog_rig != null else Vector3.ZERO

func haul_focus() -> Vector3:
	return dog_rig.haul_focus() if dog_rig != null else Vector3.ZERO

## Dev aids for scenarios: where an ard team is ploughing, where cattle graze a resting field, where an ox cart is on its trail and
## where a chariot was last placed (the origin when none).
func team_focus() -> Vector3:
	return farm_rig.team_focus() if farm_rig != null else Vector3.ZERO

func graze_focus() -> Vector3:
	return farm_rig.graze_focus() if farm_rig != null else Vector3.ZERO

func cart_focus() -> Vector3:
	return farm_rig.cart_focus() if farm_rig != null else Vector3.ZERO

func chariot_focus() -> Vector3:
	if builder == null or builder.chariot_probe == Vector2.ZERO:
		return Vector3.ZERO
	var c: Vector2 = builder.chariot_probe
	return Vector3(c.x, maxf(world.height_at(c), 0.0), c.y)

## Dev aid for scenarios: where the last decor of a kind (temple, chiefhall, woolbales, chariot ...) was placed (the origin when none).
func landmark_focus(kind: String) -> Vector3:
	if builder == null or not builder.decor_spots.has(kind):
		return Vector3.ZERO
	var c: Vector2 = builder.decor_spots[kind]
	return Vector3(c.x, maxf(world.height_at(c), 0.0), c.y)

## Dev aid for scenarios: lays the meshes of the looks projects out in rows on a plain stage beside the map, to be photographed one
## row at a time (rows are 0.7 apart, rows 0 to 4 from the nearest: people, houses and landmarks, boats and chariots, teams and carts,
## fields). Returns the stage centre.
func show_gallery() -> Vector3:
	var stage := Node3D.new()
	stage.name = "Gallery"
	var centre := Vector3(-5.0, 0.25, world.height * M7WorldData.S * 0.5)    # over the sea off the map's west edge, at the warm middle latitude
	stage.position = centre
	world_root.add_child(stage)
	var floor_mesh := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(9.0, 7.0)
	floor_mesh.mesh = plane
	floor_mesh.position = Vector3(0.0, -0.01, 0.0)
	var floor_material := StandardMaterial3D.new()
	floor_material.albedo_color = Color("#7a9a52")
	floor_material.roughness = 1.0
	floor_mesh.material_override = floor_material
	stage.add_child(floor_mesh)
	var batch := M7Batcher.new()
	batch.chunk_size = 100.0
	var rows := [
		# people: bare and tailored side by side, tool by tool
		[["person_plain", 0.18, 1.0], ["person_plain_g", 0.18, 1.0], ["person_spear", 0.18, 1.0], ["person_spear_g", 0.18, 1.0],
			["person_hoe", 0.18, 1.0], ["person_hoe_g", 0.18, 1.0], ["person_child", 0.18, 1.0], ["person_child_g", 0.18, 1.0],
			["person_elder", 0.18, 1.0], ["person_elder_g", 0.18, 1.0], ["person_crook", 0.18, 1.0], ["person_crook_g", 0.18, 1.0]],
		# landmarks and props at village scale
		[["decor_temple", 0.8, 1.45], ["decor_chiefhall", 0.8, 1.45], ["decor_woolbales", 0.5, 1.45], ["decor_chariot", 0.5, 1.45],
			["longhouse", 0.7, 1.45]],
		# boats of every age and chariots
		[["person_boat_3", 0.5, 1.0], ["person_boat_4", 0.5, 1.0], ["person_boat_5", 0.6, 1.0], ["person_boat_6", 0.6, 1.0],
			["person_chariot", 0.5, 1.0]],
		# teams and carts
		[["ard_team_a", 0.55, 1.2], ["ard_team_b", 0.55, 1.2], ["ox_cart_a", 0.65, 1.2], ["ox_cart_b", 0.65, 1.2]],
		# fields
		[["field", 1.1, 1.0], ["field_ard", 1.1, 1.0], ["field_fallow", 1.1, 1.0]],
	]
	for r in rows.size():
		var row: Array = rows[r]
		var total := 0.0
		for item in row:
			total += float(item[1])
		var x := -total * 0.5
		var z: float = [2.8, 1.5, 0.3, -0.9, -2.3][r]
		for i in row.size():
			var item: Array = row[i]
			x += float(item[1]) * 0.5
			var name: String = item[0]
			var civ := i % 4
			var tint := M7MeshBuilder.lin(M7WorldData.civ_color(civ), 0.5)
			var custom := Color(tint.r, tint.g, tint.b, 0.5)
			if name.begins_with("person") or name.begins_with("ard_") or name.begins_with("ox_"):
				custom = Color(float(civ) + 0.5 * 0.98, 0.0, 0.0, 0.0)
			var yaw := 0.0
			if name.begins_with("ard_") or name.begins_with("ox_"):
				yaw = PI * 0.5
			batch.add(name, Transform3D(Basis(Vector3.UP, yaw).scaled(Vector3.ONE * float(item[2])), Vector3(x, 0.0, z)), custom, false)
			x += float(item[1]) * 0.5
	batch.build(stage, kit, Callable(builder, "_material_for"))
	return centre

## Dev aid for scenarios: lays every building and village prop out in rows on a plain stage at village scale (see the buildings-gallery
## scenario): dwellings, yard structures, defenses and land, three rows of decor, then the landmarks. Rows are 1.4 apart (row 0 is the
## nearest, at +z). Returns the stage centre.
func show_buildings_gallery() -> Vector3:
	var stage := Node3D.new()
	stage.name = "BuildingsGallery"
	var centre := Vector3(-5.0, 0.25, world.height * M7WorldData.S * 0.5)
	stage.position = centre
	world_root.add_child(stage)
	var floor_mesh := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(8.0, 11.0)
	floor_mesh.mesh = plane
	floor_mesh.position = Vector3(0.0, -0.01, 0.0)
	var floor_material := StandardMaterial3D.new()
	floor_material.albedo_color = Color("#7a9a52")
	floor_material.roughness = 1.0
	floor_mesh.material_override = floor_material
	stage.add_child(floor_mesh)
	var batch := M7Batcher.new()
	batch.chunk_size = 100.0
	var vs := 1.45
	# [mesh, width it needs, scale]
	var rows := [
		[["tent", 0.34, vs], ["leanto", 0.36, vs], ["hide_tent", 0.34, vs], ["pithouse", 0.44, vs], ["wattle", 0.42, vs], ["mudbrick", 0.42, vs], ["longhouse", 0.7, vs], ["hall", 0.85, vs]],
		[["hearth", 0.4, vs], ["storagepit", 0.4, vs], ["dryingrack", 0.4, vs], ["granary", 0.4, vs], ["kiln", 0.4, vs], ["quern", 0.4, vs],
			["camp", 0.5, 1.0], ["lookout", 0.4, vs]],
		[["thorn", 0.4, 1.2], ["thorn", 0.4, 1.2], ["fence", 0.4, 1.15], ["weir", 0.5, 1.0], ["decor_palisade", 0.4, vs], ["beacon", 0.4, 1.25],
			["orchard_tree", 0.5, 1.0], ["orchard_tree", 0.5, 1.0]],
		[["decor_totem", 0.5, vs], ["decor_cairn", 0.5, vs], ["decor_hideframe", 0.5, vs], ["decor_pots", 0.5, vs], ["decor_loom", 0.5, vs], ["decor_shrine", 0.5, vs]],
		[["decor_signal", 0.5, vs], ["decor_charcoal", 0.5, vs], ["decor_furnace", 0.5, vs], ["decor_sledge", 0.5, vs], ["decor_spearrack", 0.5, vs], ["decor_grave", 0.5, vs]],
		[["decor_council", 0.5, vs], ["decor_quarry", 0.5, vs], ["decor_skypole", 0.5, vs], ["decor_bricks", 0.5, vs], ["decor_baskets", 0.5, vs], ["decor_woodwork", 0.5, vs]],
		[["decor_temple", 0.8, vs], ["decor_chiefhall", 0.8, vs], ["decor_woolbales", 0.5, vs], ["ziggurat", 0.8, 1.3], ["monument_menhir", 0.4, 1.3], ["monument_lintel", 0.5, 1.3]],
	]
	for r in rows.size():
		var row: Array = rows[r]
		var total := 0.0
		for item in row:
			total += float(item[1])
		var x := -total * 0.5
		var z := 4.2 - 1.4 * r
		for i in row.size():
			var item: Array = row[i]
			x += float(item[1]) * 0.5
			var name: String = item[0]
			var tint := M7MeshBuilder.lin(M7WorldData.civ_color(i % 4), 0.5)
			var custom := Color(tint.r, tint.g, tint.b, 0.5)
			var at := Vector3(x, 0.0, z)
			batch.add(name, Transform3D(Basis(Vector3.UP, 0.0).scaled(Vector3.ONE * float(item[2])), at), custom, false)
			if name == "hearth":
				batch.add("flame", Transform3D(Basis(Vector3.UP, 0.0).scaled(Vector3.ONE * vs), at + Vector3(0, 0.004, 0)), Color(1, 1, 1, 0.5), false)
			x += float(item[1]) * 0.5
	batch.build(stage, kit, Callable(builder, "_material_for"))
	return centre

## A raid or assault skirmish on the map (see skirmish_rig.gd); returns [id, seconds], or [-1, 0] when it cannot play there.
func skirmish_start(spec: Dictionary) -> Array:
	return skirmish_rig.start(spec) if skirmish_rig != null else [-1, 0.0]

func skirmish_alive(id: int) -> bool:
	return skirmish_rig != null and skirmish_rig.alive(id)

func skirmish_clear() -> void:
	if skirmish_rig != null:
		skirmish_rig.clear()

## Dev aid for scenarios: where the latest skirmish is being fought (the origin when none).
func skirmish_focus() -> Vector3:
	return skirmish_rig.focus() if skirmish_rig != null else Vector3.ZERO

## Dev aid for scenarios: where a feast is under way now (the origin when none).
func feast_focus() -> Vector3:
	if feast_rig == null:
		return Vector3.ZERO
	for site in feast_rig.sites:
		if site.shown:
			var c: Vector2 = site.centre
			return Vector3(c.x, maxf(world.height_at(c), 0.0), c.y)
	return Vector3.ZERO

func _process(_delta: float) -> void:
	if builder != null:
		builder.update_herds()
	if skirmish_rig != null and not skirmish_rig.fights.is_empty():
		skirmish_rig.update(_delta, clampf(1.25 + (camera_distance - 8.0) / 24.0, 1.25, 2.6), clampf(camera_distance / 12.0, 1.0, 6.0), camera.global_position if camera != null else Vector3.ZERO)
	if signal_rig != null:
		signal_rig.update(Time.get_ticks_msec() / 1000.0)

func set_season(season: int) -> void:
	_apply_season(season)
	if mats.has("terrain"):
		mats.terrain.set_shader_parameter("overlay_forecast", 1.0 if season >= 2 else 0.0)

func set_overlay(mode: int) -> void:
	overlay_mode = clampi(mode, 0, 7)
	if mats.has("terrain"):
		mats.terrain.set_shader_parameter("overlay_mode", overlay_mode)
		mats.terrain.set_shader_parameter("claim_overlay_strength", 1.35 if overlay_mode == 1 else 0.0)

func set_camera(target: Vector3, yaw: float, pitch: float, distance: float) -> void:
	if camera == null:
		return
	camera_focus = target
	camera_distance = distance
	if builder != null and builder.trail_node != null:
		# tracks widen as the camera pulls back, so a footpath is still there at region zoom
		(builder.trail_node.material_override as ShaderMaterial).set_shader_parameter("width_boost", clampf(distance / 16.0, 1.0, 3.6))
	var regional := distance > 38.0
	if builder != null:
		builder.set_regional_lod(regional)
	if regional != regional_render_scale_active:
		regional_render_scale_active = regional
		var viewport := get_viewport()
		viewport.scaling_3d_mode = Viewport.SCALING_3D_MODE_FSR
		viewport.scaling_3d_scale = 0.75 if regional else 1.0
	var off := Vector3(sin(yaw) * cos(pitch), sin(pitch), cos(yaw) * cos(pitch)) * distance
	camera.position = target + off
	camera.look_at(target, Vector3.UP)
	sun.rotation = Vector3(deg_to_rad(-38.0 if world.season != 3 else -28.0), yaw - deg_to_rad(40.0), 0)
	sun.directional_shadow_max_distance = clampf(distance * 1.7, 10.0, 220.0)
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS if distance < 10.0 else DirectionalLight3D.SHADOW_PARALLEL_2_SPLITS
	RenderingServer.global_shader_parameter_set("fig_scale", clampf(1.25 + (distance - 8.0) / 24.0, 1.25, 2.6))
	environment.fog_depth_begin = distance * 1.05
	environment.fog_depth_end = distance * 3.2
	environment.fog_depth_curve = 1.4
	environment.fog_density = 0.35 if distance < 120.0 else 0.25
	camera_attributes.dof_blur_far_enabled = distance < 10.0
	camera_attributes.dof_blur_far_distance = distance * 1.35
	camera_attributes.dof_blur_far_transition = distance * 0.9
	camera_attributes.dof_blur_near_enabled = distance < 10.0
	camera_attributes.dof_blur_near_distance = distance * 0.55
	camera_attributes.dof_blur_near_transition = distance * 0.3
	camera_attributes.dof_blur_amount = 0.05
	environment.ssao_radius = clampf(distance * 0.012, 0.12, 1.2)
	environment.ssao_enabled = distance < 10.0
	mats.terrain.set_shader_parameter("territory_strength", smoothstep(7.0, 60.0, distance) * 1.5 + smoothstep(60.0, 150.0, distance) * 0.9)
	mats.border.set_shader_parameter("glow_strength", 0.45 + 0.55 * smoothstep(6.0, 40.0, distance))
	var far := distance > 70.0
	world_root.get_node("Borders").visible = not far
	world_root.get_node("BordersFar").visible = far
	var detail := 1.0 if distance < 8.0 else (0.5 if distance < 24.0 else 0.0)
	mats.terrain.set_shader_parameter("terrain_detail", detail)
	mats.water.set_shader_parameter("water_detail", 1.0 if distance < 24.0 else 0.0)
	RenderingServer.global_shader_parameter_set("cloud_amount", 1.0 if distance < 24.0 else 0.0)
	_set_villages()

func _make_state_texture() -> void:
	state_image = Image.create(world.width, world.height, false, Image.FORMAT_RGBA8)
	state_texture = ImageTexture.create_from_image(state_image)
	_update_state_texture()

func _make_overlay_texture() -> void:
	var data := PackedByteArray()
	data.resize(world.width * world.height * 4)
	overlay_image = Image.create_from_data(world.width, world.height, false, Image.FORMAT_RGBA8, data)
	overlay_texture = ImageTexture.create_from_image(overlay_image)
	e3_overlay_image = Image.create(world.width, world.height, false, Image.FORMAT_RGBA8)
	e3_overlay_texture = ImageTexture.create_from_image(e3_overlay_image)
	forest_overlay_image = Image.create(world.width, world.height, false, Image.FORMAT_RGBA8)
	forest_overlay_texture = ImageTexture.create_from_image(forest_overlay_image)
	_update_overlay_texture()
	_update_e3_overlay_texture()
	_update_forest_overlay_texture()

func _update_overlay_texture() -> void:
	if overlay_image == null or world == null:
		return
	if world.overlay_bytes.size() == world.width * world.height * 4:
		overlay_image.set_data(world.width, world.height, false, Image.FORMAT_RGBA8, world.overlay_bytes)
		overlay_texture.update(overlay_image)
		if mats.has("terrain"):
			mats.terrain.set_shader_parameter("overlay_state", overlay_texture)
		return
	var data := PackedByteArray()
	data.resize(world.width * world.height * 4)
	for t in world.width * world.height:
		var o := t * 4
		data[o] = world.food_overlay[t] if t < world.food_overlay.size() else 0
		data[o + 1] = world.forecast_overlay[t] if t < world.forecast_overlay.size() else 0
		data[o + 2] = world.method_overlay[t] if t < world.method_overlay.size() else 0
		data[o + 3] = world.method_fallback_overlay[t] if t < world.method_fallback_overlay.size() else 0
	overlay_image.set_data(world.width, world.height, false, Image.FORMAT_RGBA8, data)
	overlay_texture.update(overlay_image)
	if mats.has("terrain"):
		mats.terrain.set_shader_parameter("overlay_state", overlay_texture)

func _update_e3_overlay_texture() -> void:
	if e3_overlay_image == null or world == null:
		return
	if world.e3_overlay_bytes.size() == world.width * world.height * 4:
		e3_overlay_image.set_data(world.width, world.height, false, Image.FORMAT_RGBA8, world.e3_overlay_bytes)
		e3_overlay_texture.update(e3_overlay_image)
		if mats.has("terrain"):
			mats.terrain.set_shader_parameter("e3_overlay_state", e3_overlay_texture)
	elif e3_overlay_texture != null:
		var blank := PackedByteArray()
		blank.resize(world.width * world.height * 4)
		e3_overlay_image.set_data(world.width, world.height, false, Image.FORMAT_RGBA8, blank)
		e3_overlay_texture.update(e3_overlay_image)

func _update_forest_overlay_texture() -> void:
	if forest_overlay_image == null or world == null:
		return
	var data := PackedByteArray()
	data.resize(world.width * world.height * 4)
	for t in range(world.width * world.height):
		var o := t * 4
		var band: int = world.forest_overlay[t] if t < world.forest_overlay.size() else 0
		data[o] = band
		data[o + 3] = 255
	forest_overlay_image.set_data(world.width, world.height, false, Image.FORMAT_RGBA8, data)
	forest_overlay_texture.update(forest_overlay_image)
	if mats.has("terrain"):
		mats.terrain.set_shader_parameter("forest_overlay_state", forest_overlay_texture)

func _update_state_texture() -> void:
	if world.state_bytes.size() == world.width * world.height * 4:
		state_image.set_data(world.width, world.height, false, Image.FORMAT_RGBA8, world.state_bytes)
		state_texture.update(state_image)
		if mats.has("terrain"):
			mats.terrain.set_shader_parameter("tile_state", state_texture)
		return
	for t in world.width * world.height:
		var owner_code := clampi(world.owner[t] + 1, 0, 255)
		state_image.set_pixel(t % world.width, t / world.width,
			Color(owner_code / 255.0, world.claim[t] / 255.0, world.visibility[t] * 0.5,
				clampi(world.claimant[t] + 1, 0, 255) / 255.0))
	state_texture.update(state_image)
	if mats.has("terrain"):
		mats.terrain.set_shader_parameter("tile_state", state_texture)

func _make_materials() -> void:
	var civ_colors := PackedVector3Array()
	for i in 8:
		var c := M7WorldData.civ_color(i).srgb_to_linear()
		civ_colors.append(Vector3(c.r, c.g, c.b))
	var terrain := ShaderMaterial.new()
	terrain.shader = load("res://Renderer/LowPoly3D/Shaders/terrain.gdshader")
	terrain.set_shader_parameter("civ_col", civ_colors)
	terrain.set_shader_parameter("tile_state", state_texture)
	terrain.set_shader_parameter("overlay_state", overlay_texture)
	terrain.set_shader_parameter("e3_overlay_state", e3_overlay_texture)
	terrain.set_shader_parameter("forest_overlay_state", forest_overlay_texture)
	terrain.set_shader_parameter("overlay_forecast", 1.0 if world.season >= 2 else 0.0)
	mats.terrain = terrain
	set_overlay(overlay_mode)
	var water := ShaderMaterial.new()
	water.shader = load("res://Renderer/LowPoly3D/Shaders/water.gdshader")
	mats.water = water
	var river := ShaderMaterial.new()
	river.shader = load("res://Renderer/LowPoly3D/Shaders/river.gdshader")
	mats.river = river
	var border := ShaderMaterial.new()
	border.shader = load("res://Renderer/LowPoly3D/Shaders/border.gdshader")
	border.render_priority = 1
	mats.border = border
	var prop := ShaderMaterial.new()
	prop.shader = load("res://Renderer/LowPoly3D/Shaders/prop.gdshader")
	mats.prop = prop
	var prop_wind := ShaderMaterial.new()
	prop_wind.shader = prop.shader
	prop_wind.set_shader_parameter("wind", 0.35)
	prop_wind.set_shader_parameter("burn_hide", 2)      # trees burn away
	mats.prop_wind = prop_wind
	var grass_wind := ShaderMaterial.new()
	grass_wind.shader = prop.shader
	grass_wind.set_shader_parameter("wind", 0.35)
	grass_wind.set_shader_parameter("burn_hide", 1)     # grass and low plants burn away
	mats.grass_wind = grass_wind
	var banner := ShaderMaterial.new()
	banner.shader = prop.shader
	banner.set_shader_parameter("wind", 0.25)
	banner.set_shader_parameter("lod_grow", 1.5)
	banner.set_shader_parameter("billboard_y", true)
	mats.banner = banner
	var figure := ShaderMaterial.new()
	var deposit := ShaderMaterial.new()                # grows with the camera distance like people do, so a deposit still reads zoomed out
	deposit.shader = prop.shader
	deposit.set_shader_parameter("lod_grow", 1.2)
	mats.deposit = deposit
	figure.shader = prop.shader
	figure.set_shader_parameter("is_figure", true)
	var figure_tints := PackedVector3Array()
	for civ in 8:
		var lin := M7WorldData.civ_color(civ).srgb_to_linear()
		figure_tints.append(Vector3(lin.r, lin.g, lin.b))
	var free_folk := Color("#9a9a9a").srgb_to_linear()
	figure_tints.append(Vector3(free_folk.r, free_folk.g, free_folk.b))
	figure_tints.append(Vector3.ZERO)
	figure.set_shader_parameter("fig_tints", figure_tints)
	mats.figure = figure
	var animal := ShaderMaterial.new()
	animal.shader = prop.shader
	animal.set_shader_parameter("is_animal", true)
	var animal_tints := PackedVector3Array()
	for sp in M7WorldBuilder.SPECIES.size():
		for bin in 8:
			var hv := (bin + 0.5) / 8.0
			var lin := M7MeshBuilder.lin(Color(M7WorldBuilder.SPECIES[sp][1]).lightened(0.12 * (hv - 0.5)), 0.5)
			animal_tints.append(Vector3(lin.r, lin.g, lin.b))
	while animal_tints.size() < 80:
		animal_tints.append(Vector3.ZERO)
	animal.set_shader_parameter("animal_tints", animal_tints)
	mats.animal = animal
	var fire := ShaderMaterial.new()
	fire.shader = load("res://Renderer/LowPoly3D/Shaders/fire.gdshader")
	mats.fire = fire
	var shadow := ShaderMaterial.new()
	shadow.shader = load("res://Renderer/LowPoly3D/Shaders/shadow_disc.gdshader")
	mats.shadow_disc = shadow
	var smoke := ShaderMaterial.new()
	smoke.shader = load("res://Renderer/LowPoly3D/Shaders/smoke.gdshader")
	mats.smoke = smoke

func _make_environment() -> void:
	var world_environment := WorldEnvironment.new()
	environment = Environment.new()
	var sky := Sky.new()
	var sky_material := ProceduralSkyMaterial.new()
	sky_material.sky_top_color = Color("#5f93c9")
	sky_material.sky_horizon_color = Color("#cfe0e6")
	sky_material.ground_horizon_color = Color("#b9cbd0")
	sky_material.ground_bottom_color = Color("#56707a")
	sky_material.sun_angle_max = 20.0
	sky.sky_material = sky_material
	environment.sky = sky
	environment.background_mode = Environment.BG_SKY
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	environment.ambient_light_color = Color("#8fb3dc")
	environment.ambient_light_sky_contribution = 0.55
	environment.ambient_light_energy = 0.75
	environment.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	environment.tonemap_mode = Environment.TONE_MAPPER_ACES
	environment.tonemap_exposure = 1.05
	environment.tonemap_white = 6.0
	environment.ssao_enabled = true
	environment.ssao_radius = 0.35
	environment.ssao_intensity = 2.2
	environment.ssao_power = 1.6
	environment.ssao_detail = 0.6
	environment.ssao_horizon = 0.06
	environment.ssao_light_affect = 0.15
	environment.ssil_enabled = false
	environment.glow_enabled = true
	environment.glow_intensity = 0.55
	environment.glow_strength = 1.0
	environment.glow_bloom = 0.04
	environment.glow_hdr_threshold = 1.1
	environment.glow_blend_mode = Environment.GLOW_BLEND_MODE_SOFTLIGHT
	environment.fog_enabled = true
	environment.fog_mode = Environment.FOG_MODE_DEPTH
	environment.fog_light_color = Color("#c9dbe2")
	environment.fog_density = 0.6
	environment.fog_sky_affect = 0.6
	environment.fog_aerial_perspective = 0.25
	environment.adjustment_enabled = true
	environment.adjustment_brightness = 1.0
	environment.adjustment_contrast = 1.06
	environment.adjustment_saturation = 1.08
	world_environment.environment = environment
	add_child(world_environment)
	sun = DirectionalLight3D.new()
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	sun.directional_shadow_blend_splits = true
	sun.shadow_bias = 0.03
	sun.shadow_normal_bias = 0.8
	sun.light_angular_distance = 0.0
	sun.shadow_blur = 1.6
	add_child(sun)

func _make_camera() -> void:
	camera = Camera3D.new()
	camera.name = "Camera3D"
	camera.fov = 30.0
	camera.near = 0.05
	camera.far = 900.0
	camera_attributes = CameraAttributesPractical.new()
	camera.attributes = camera_attributes
	add_child(camera)
	camera.make_current()

func _make_grade_overlay() -> void:
	var layer := CanvasLayer.new()
	layer.layer = 0
	var rect := ColorRect.new()
	rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var grade := ShaderMaterial.new()
	grade.shader = load("res://Renderer/LowPoly3D/Shaders/grade.gdshader")
	rect.material = grade
	layer.add_child(rect)
	add_child(layer)

func _apply_season(season: int) -> void:
	if season == last_season:
		return
	last_season = season
	var spring := 0.0
	var autumn := 0.0
	var snow := 0.0
	var dry := 0.0
	var line := 1.3
	match season:
		0:
			spring = 1.0
			line = 0.86
		1:
			dry = 1.0
			line = 1.3
		2:
			autumn = 1.0
			line = 0.9
		3:
			snow = 1.0
			line = 0.2
	RenderingServer.global_shader_parameter_set("season_spring", spring)
	RenderingServer.global_shader_parameter_set("season_autumn", autumn)
	RenderingServer.global_shader_parameter_set("season_snow", snow)
	RenderingServer.global_shader_parameter_set("season_dry", dry)
	RenderingServer.global_shader_parameter_set("snow_line", line)
	sun.light_color = Color("#ffe8c6") if season != 3 else Color("#f2f2ff")
	sun.light_energy = 1.8 if season != 3 else 1.5
	for fx in hearth_fx_by_tile.values():
		var light := fx.get("light") as OmniLight3D
		if is_instance_valid(light):
			_set_hearth_light(light, season)

func _set_villages() -> void:
	var values := PackedVector4Array()
	var limit := 8 if camera_distance < 10.0 else (12 if camera_distance < 24.0 else (6 if camera_distance > 38.0 else 16))
	var nearest: Array = builder.villages.duplicate()
	nearest.sort_custom(func(a: Vector3, b: Vector3) -> bool:
		return Vector2(a.x - camera_focus.x, a.y - camera_focus.z).length_squared() < \
			Vector2(b.x - camera_focus.x, b.y - camera_focus.z).length_squared())
	for v in nearest:
		if values.size() >= limit:
			break
		values.append(Vector4(v.x, v.y, v.z, 0.0))
	while values.size() < 48:
		values.append(Vector4(-1000.0, -1000.0, 0.0, 0.0))
	mats.terrain.set_shader_parameter("villages", values)
	mats.terrain.set_shader_parameter("village_count", mini(builder.villages.size(), limit))

func _sync_hearth_fx() -> void:
	var active_tiles := {}
	for i in builder.hearths.size():
		var hearth: Vector3 = builder.hearths[i]
		var tile := int(builder.w.settlements[i].tile)
		active_tiles[tile] = true
		var fx: Dictionary = hearth_fx_by_tile.get(tile, {})
		var light := fx.get("light") as OmniLight3D
		if not is_instance_valid(light):
			light = OmniLight3D.new()
			light.light_color = Color("#ff9440")
			light.omni_attenuation = 1.6
			light.shadow_enabled = false
			world_root.add_child(light)
			fx["light"] = light
		_set_hearth_light(light, world.season)
		light.position = hearth + Vector3(0.0, 0.08, 0.0)
		var smoke := fx.get("smoke") as GPUParticles3D
		if not is_instance_valid(smoke):
			smoke = _make_smoke()
			world_root.add_child(smoke)
			fx["smoke"] = smoke
		smoke.position = hearth + Vector3(0.0, 0.07, 0.0)
		hearth_fx_by_tile[tile] = fx
	for tile in hearth_fx_by_tile.keys():
		if active_tiles.has(tile):
			continue
		var fx: Dictionary = hearth_fx_by_tile[tile]
		var light := fx.get("light") as OmniLight3D
		if is_instance_valid(light):
			light.queue_free()
		var smoke := fx.get("smoke") as GPUParticles3D
		if is_instance_valid(smoke):
			smoke.queue_free()
		hearth_fx_by_tile.erase(tile)

func _set_hearth_light(light: OmniLight3D, season: int) -> void:
	var energy := 0.18 if season == 3 else 1.2
	var light_range := 0.45 if season == 3 else 0.75
	if light.light_energy != energy:
		light.light_energy = energy
	if light.omni_range != light_range:
		light.omni_range = light_range

func _make_smoke() -> GPUParticles3D:
	var particles := GPUParticles3D.new()
	particles.amount = 14
	particles.lifetime = 5.0
	particles.preprocess = 6.0
	particles.local_coords = false
	var process_material := ParticleProcessMaterial.new()
	process_material.direction = Vector3(0.15, 1.0, 0.0)
	process_material.spread = 8.0
	process_material.initial_velocity_min = 0.05
	process_material.initial_velocity_max = 0.08
	process_material.gravity = Vector3(0.03, 0.005, 0.01)
	process_material.scale_min = 0.6
	process_material.scale_max = 1.0
	var curve := Curve.new()
	curve.add_point(Vector2(0.0, 0.35))
	curve.add_point(Vector2(1.0, 2.4))
	var curve_texture := CurveTexture.new()
	curve_texture.curve = curve
	process_material.scale_curve = curve_texture
	var gradient := Gradient.new()
	gradient.set_color(0, Color(1, 1, 1, 0))
	gradient.set_color(1, Color(1, 1, 1, 0))
	gradient.add_point(0.12, Color(1, 1, 1, 0.75))
	gradient.add_point(0.6, Color(1, 1, 1, 0.35))
	var gradient_texture := GradientTexture1D.new()
	gradient_texture.gradient = gradient
	process_material.color_ramp = gradient_texture
	particles.process_material = process_material
	var quad := QuadMesh.new()
	quad.size = Vector2(0.07, 0.07)
	quad.material = mats.smoke
	particles.draw_pass_1 = quad
	particles.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	particles.visibility_aabb = AABB(Vector3(-1, -0.5, -1), Vector3(2, 3, 2))
	return particles
