## The production low-poly renderer. C# owns the simulation and supplies immutable
## viewer-filtered MatchFrame snapshots; this script only builds and updates visuals.
extends Node3D

const M7WorldData = preload("res://Renderer/LowPoly3D/Scripts/world_data.gd")
const M7MeshKit = preload("res://Renderer/LowPoly3D/Scripts/mesh_kit.gd")
const M7WorldBuilder = preload("res://Renderer/LowPoly3D/Scripts/world_builder.gd")

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
var overlay_mode := 0 # 0 none, 1 claim strength, 2 food security, 3 methods
var fire_lights: Array[OmniLight3D] = []
var smoke_nodes: Array[Node3D] = []
var last_season := -1
var golden_hour := false
var camera_focus := Vector3.ZERO
var camera_distance := 40.0
var regional_render_scale_active := false

func setup_frame(frame_data: Dictionary) -> void:
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
	var tile_state := Time.get_ticks_usec()
	_apply_season(world.season)
	if mats.has("terrain"):
		mats.terrain.set_shader_parameter("overlay_forecast", 1.0 if world.season >= 2 else 0.0)
	var structures_changed := builder.update_frame()
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
func set_render_tick(tick: float) -> void:
	if builder != null and builder.rig != null:
		builder.rig.update(tick)

## Dev aid for scenarios: the first monument's ring centre in world coordinates (or the origin when there is none).
func monument_focus() -> Vector3:
	if builder == null or builder.rig == null or builder.rig.sites.is_empty():
		return Vector3.ZERO
	var c: Vector2 = builder.rig.sites[0].center
	return Vector3(c.x, maxf(world.height_at(c), 0.0), c.y)

func _process(_delta: float) -> void:
	if builder != null:
		builder.update_herds()

func set_season(season: int) -> void:
	_apply_season(season)
	if mats.has("terrain"):
		mats.terrain.set_shader_parameter("overlay_forecast", 1.0 if season >= 2 else 0.0)

func set_overlay(mode: int) -> void:
	overlay_mode = clampi(mode, 0, 3)
	if mats.has("terrain"):
		mats.terrain.set_shader_parameter("overlay_mode", overlay_mode)
		mats.terrain.set_shader_parameter("claim_overlay_strength", 1.35 if overlay_mode == 1 else 0.0)

func set_camera(target: Vector3, yaw: float, pitch: float, distance: float) -> void:
	if camera == null:
		return
	camera_focus = target
	camera_distance = distance
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
	_update_overlay_texture()

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
	mats.prop_wind = prop_wind
	var banner := ShaderMaterial.new()
	banner.shader = prop.shader
	banner.set_shader_parameter("wind", 0.25)
	banner.set_shader_parameter("lod_grow", 1.5)
	banner.set_shader_parameter("billboard_y", true)
	mats.banner = banner
	var figure := ShaderMaterial.new()
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
	for light in fire_lights:
		light.queue_free()
	for smoke in smoke_nodes:
		smoke.queue_free()
	fire_lights.clear()
	smoke_nodes.clear()
	for hearth in builder.hearths:
		var light := OmniLight3D.new()
		light.light_color = Color("#ff9440")
		light.light_energy = 0.18 if world.season == 3 else 1.2
		light.omni_range = 0.45 if world.season == 3 else 0.75
		light.omni_attenuation = 1.6
		light.position = hearth + Vector3(0.0, 0.08, 0.0)
		light.shadow_enabled = false
		world_root.add_child(light)
		fire_lights.append(light)
		var smoke := _make_smoke()
		smoke.position = hearth + Vector3(0.0, 0.07, 0.0)
		world_root.add_child(smoke)
		smoke_nodes.append(smoke)

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
