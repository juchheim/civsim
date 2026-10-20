## CivSim low-poly 3D diorama prototype.
## Interactive: drag (left/right mouse) to orbit, wheel to zoom, WASD/arrows to pan, Q/E rotate,
## 1-4 season, 5/6 frame20/frame40, T toggles golden hour, B toggles borders, F frames the capital.
## Shot mode: `godot-mono --path . --resolution 1600x900 -- --shot` renders shots/ and quits.
extends Node3D

const FRAMES := {"frame20": "res://data/frame20.json", "frame40": "res://data/frame40.json"}

var world := WorldData.new()
var kit: MeshKit
var mats := {}
var world_root: Node3D
var builder: WorldBuilder
var sun: DirectionalLight3D
var env: Environment
var cam: Camera3D
var cam_attr: CameraAttributesPractical
var fire_lights: Array[OmniLight3D] = []
var smoke_nodes: Array[Node3D] = []
var hud: Label

var target := Vector3.ZERO
var yaw := 0.0
var pitch := deg_to_rad(45.0)
var dist := 30.0
var season := 0
var golden := false
var sun_pitch := -38.0
var sun_rel_yaw := -40.0
var dragging := false
var borders_on := true
var current_frame := "frame40"

func _ready() -> void:
	DisplayServer.window_set_title("CivSim low-poly diorama prototype")
	kit = MeshKit.new()
	_make_materials()
	_make_environment()
	_make_camera()
	_make_hud()
	_load_frame(current_frame)
	_apply_season(world.season)
	_apply_lighting()
	_frame_settlement(0, 30.0, 45.0)
	if "--shot" in OS.get_cmdline_user_args():
		_shoot.call_deferred()

# ------------------------------------------------------------------ setup

func _make_materials() -> void:
	var civ := PackedVector3Array()
	for i in 8:
		var c := WorldData.civ_color(i).srgb_to_linear()
		civ.append(Vector3(c.r, c.g, c.b))
	var terrain := ShaderMaterial.new()
	terrain.shader = load("res://shaders/terrain.gdshader")
	terrain.set_shader_parameter("civ_col", civ)
	mats.terrain = terrain
	var water := ShaderMaterial.new()
	water.shader = load("res://shaders/water.gdshader")
	mats.water = water
	var river := ShaderMaterial.new()
	river.shader = load("res://shaders/river.gdshader")
	mats.river = river
	var border := ShaderMaterial.new()
	border.shader = load("res://shaders/border.gdshader")
	border.render_priority = 1
	mats.border = border
	var prop := ShaderMaterial.new()
	prop.shader = load("res://shaders/prop.gdshader")
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
	var fig := ShaderMaterial.new()
	fig.shader = prop.shader
	fig.set_shader_parameter("is_figure", true)
	mats.figure = fig
	var fire := ShaderMaterial.new()
	fire.shader = load("res://shaders/fire.gdshader")
	mats.fire = fire
	var sd := ShaderMaterial.new()
	sd.shader = load("res://shaders/shadow_disc.gdshader")
	mats.shadow_disc = sd
	var smoke := ShaderMaterial.new()
	smoke.shader = load("res://shaders/smoke.gdshader")
	mats.smoke = smoke

func _make_environment() -> void:
	var we := WorldEnvironment.new()
	env = Environment.new()
	var sky := Sky.new()
	var sm := ProceduralSkyMaterial.new()
	sm.sky_top_color = Color("#5f93c9")
	sm.sky_horizon_color = Color("#cfe0e6")
	sm.ground_horizon_color = Color("#b9cbd0")
	sm.ground_bottom_color = Color("#56707a")
	sm.sun_angle_max = 20.0
	sky.sky_material = sm
	env.sky = sky
	env.background_mode = Environment.BG_SKY
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_color = Color("#8fb3dc")
	env.ambient_light_sky_contribution = 0.55
	env.ambient_light_energy = 0.75
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 1.05
	env.tonemap_white = 6.0
	env.ssao_enabled = true
	env.ssao_radius = 0.35
	env.ssao_intensity = 2.2
	env.ssao_power = 1.6
	env.ssao_detail = 0.6
	env.ssao_horizon = 0.06
	env.ssao_light_affect = 0.15
	env.ssil_enabled = false
	env.glow_enabled = true
	env.glow_intensity = 0.55
	env.glow_strength = 1.0
	env.glow_bloom = 0.04
	env.glow_hdr_threshold = 1.1
	env.glow_blend_mode = Environment.GLOW_BLEND_MODE_SOFTLIGHT
	env.fog_enabled = true
	env.fog_mode = Environment.FOG_MODE_DEPTH
	env.fog_light_color = Color("#c9dbe2")
	env.fog_density = 0.6
	env.fog_sky_affect = 0.6
	env.fog_aerial_perspective = 0.25
	env.adjustment_enabled = true
	env.adjustment_brightness = 1.0
	env.adjustment_contrast = 1.06
	env.adjustment_saturation = 1.08
	we.environment = env
	add_child(we)
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
	cam = Camera3D.new()
	cam.fov = 30.0
	cam.near = 0.05
	cam.far = 900.0
	cam_attr = CameraAttributesPractical.new()
	cam.attributes = cam_attr
	add_child(cam)
	cam.make_current()

func _make_hud() -> void:
	var grade_layer := CanvasLayer.new()
	grade_layer.layer = 0
	var rect := ColorRect.new()
	rect.set_anchors_preset(Control.PRESET_FULL_RECT)
	rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var gm := ShaderMaterial.new()
	gm.shader = load("res://shaders/grade.gdshader")
	rect.material = gm
	grade_layer.add_child(rect)
	add_child(grade_layer)
	var layer := CanvasLayer.new()
	layer.layer = 1
	hud = Label.new()
	hud.position = Vector2(14, 10)
	hud.add_theme_color_override("font_color", Color(1, 1, 1, 0.85))
	hud.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.6))
	hud.add_theme_constant_override("shadow_offset_x", 1)
	hud.add_theme_constant_override("shadow_offset_y", 1)
	layer.add_child(hud)
	add_child(layer)

func _load_frame(name: String) -> void:
	current_frame = name
	if world_root:
		world_root.queue_free()
	for l in fire_lights:
		l.queue_free()
	fire_lights.clear()
	smoke_nodes.clear()
	world = WorldData.new()
	world.load_json(FRAMES[name])
	RenderingServer.global_shader_parameter_set("map_lat", Vector2(world.height * WorldData.S * 0.5, world.height * WorldData.S * 0.5))
	builder = WorldBuilder.new(world, kit, mats)
	world_root = builder.build()
	add_child(world_root)
	print("built %s: %s" % [name, str(builder.stats)])
	var vs := PackedFloat32Array()
	for v in builder.villages:
		vs.append_array([v.x, v.y, v.z, 0.0])
	while vs.size() < 48 * 4:
		vs.append_array([-1000.0, -1000.0, 0.0, 0.0])
	vs.resize(48 * 4)
	mats.terrain.set_shader_parameter("villages", vs)
	mats.terrain.set_shader_parameter("village_count", mini(builder.villages.size(), 48))
	# per-hearth firelight and smoke
	for i in builder.hearths.size():
		var hp: Vector3 = builder.hearths[i]
		var l := OmniLight3D.new()
		l.light_color = Color("#ff9440")
		l.light_energy = 1.2
		l.omni_range = 0.75
		l.omni_attenuation = 1.6
		l.position = hp + Vector3(0, 0.08, 0)
		l.shadow_enabled = false
		world_root.add_child(l)
		fire_lights.append(l)
		var sm := _make_smoke()
		sm.position = hp + Vector3(0, 0.07, 0)
		world_root.add_child(sm)
		smoke_nodes.append(sm)

func _make_smoke() -> GPUParticles3D:
	var p := GPUParticles3D.new()
	p.amount = 14
	p.lifetime = 5.0
	p.preprocess = 6.0
	p.local_coords = false
	var pm := ParticleProcessMaterial.new()
	pm.direction = Vector3(0.15, 1, 0)
	pm.spread = 8.0
	pm.initial_velocity_min = 0.05
	pm.initial_velocity_max = 0.08
	pm.gravity = Vector3(0.03, 0.005, 0.01)
	pm.scale_min = 0.6
	pm.scale_max = 1.0
	var curve := Curve.new()
	curve.add_point(Vector2(0, 0.35))
	curve.add_point(Vector2(1, 2.4))
	var ct := CurveTexture.new()
	ct.curve = curve
	pm.scale_curve = ct
	var grad := Gradient.new()
	grad.set_color(0, Color(1, 1, 1, 0.0))
	grad.set_color(1, Color(1, 1, 1, 0.0))
	grad.add_point(0.12, Color(1, 1, 1, 0.75))
	grad.add_point(0.6, Color(1, 1, 1, 0.35))
	var gt := GradientTexture1D.new()
	gt.gradient = grad
	pm.color_ramp = gt
	p.process_material = pm
	var q := QuadMesh.new()
	q.size = Vector2(0.07, 0.07)
	q.material = mats.smoke
	p.draw_pass_1 = q
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	p.visibility_aabb = AABB(Vector3(-1, -0.5, -1), Vector3(2, 3, 2))
	return p

# ------------------------------------------------------------------ season and light

func _apply_season(s: int) -> void:
	season = s
	var sp := 0.0
	var au := 0.0
	var sn := 0.0
	var dry := 0.0
	var line := 1.3
	match s:
		0:
			sp = 1.0
			line = 0.86
		1:
			dry = 1.0
			line = 1.3
		2:
			au = 1.0
			line = 0.9
		3:
			sn = 1.0
			line = 0.2
	RenderingServer.global_shader_parameter_set("season_spring", sp)
	RenderingServer.global_shader_parameter_set("season_autumn", au)
	RenderingServer.global_shader_parameter_set("season_snow", sn)
	RenderingServer.global_shader_parameter_set("season_dry", dry)
	RenderingServer.global_shader_parameter_set("snow_line", line)
	_apply_lighting()

func _apply_lighting() -> void:
	var sm: ProceduralSkyMaterial = env.sky.sky_material
	if golden:
		sun_pitch = -16.0
		sun_rel_yaw = -78.0
		sun.light_color = Color("#ffa95a")
		sun.light_energy = 2.7
		env.ambient_light_energy = 0.5
		env.ambient_light_color = Color("#b58f86")
		env.ambient_light_sky_contribution = 0.35
		mats.border.set_shader_parameter("line_boost", 1.3)
		sm.sky_top_color = Color("#4f6d9c")
		sm.sky_horizon_color = Color("#f2b98a")
		sm.ground_horizon_color = Color("#d9a27a")
		env.fog_light_color = Color("#e7b48d")
		env.adjustment_saturation = 1.12
		for l in fire_lights:
			l.light_energy = 2.4
			l.omni_range = 1.1
	else:
		var winter := season == 3
		sun_pitch = -38.0 if not winter else -28.0
		sun_rel_yaw = -40.0
		sun.light_color = Color("#ffe8c6") if not winter else Color("#f2f2ff")
		sun.light_energy = 1.8 if not winter else 1.5
		env.ambient_light_energy = 0.55
		env.ambient_light_color = Color("#8fb3dc")
		env.ambient_light_sky_contribution = 0.55
		mats.border.set_shader_parameter("line_boost", 2.2)
		sm.sky_top_color = Color("#5f93c9") if not winter else Color("#7c97b3")
		sm.sky_horizon_color = Color("#d6e4e8") if not winter else Color("#dfe6ea")
		sm.ground_horizon_color = Color("#b9cbd0")
		env.fog_light_color = Color("#cfe0e6") if not winter else Color("#dfe7ec")
		env.adjustment_saturation = 1.05 if not winter else 0.88
		for l in fire_lights:
			l.light_energy = 0.18
			l.omni_range = 0.45
	_update_camera()

# ------------------------------------------------------------------ camera

func _frame_settlement(i: int, d: float, pitch_deg: float) -> void:
	var h: Vector3 = builder.hearths[i] if i < builder.hearths.size() else Vector3(80, 0, 43)
	target = h
	dist = d
	pitch = deg_to_rad(pitch_deg)
	_update_camera()

func _update_camera() -> void:
	var off := Vector3(sin(yaw) * cos(pitch), sin(pitch), cos(yaw) * cos(pitch)) * dist
	cam.position = target + off
	cam.look_at(target, Vector3.UP)
	# the sun keeps its angle to the view (light from behind-left), like a lit diorama
	sun.rotation = Vector3(deg_to_rad(sun_pitch), yaw + deg_to_rad(sun_rel_yaw), 0)
	# figures grow a little when zoomed out so they stay readable (Z1/Z2)
	RenderingServer.global_shader_parameter_set("fig_scale", clampf(1.25 + (dist - 8.0) / 24.0, 1.25, 2.6))
	sun.directional_shadow_max_distance = clampf(dist * 1.7, 10.0, 220.0)
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS if dist < 60.0 else DirectionalLight3D.SHADOW_PARALLEL_2_SPLITS
	env.fog_depth_begin = dist * 1.05
	env.fog_depth_end = dist * 3.2
	env.fog_depth_curve = 1.4
	env.fog_density = 0.35 if dist < 120.0 else 0.25
	# gentle tilt-shift: blur the far band (and a little of the near band)
	cam_attr.dof_blur_far_enabled = true
	cam_attr.dof_blur_far_distance = dist * 1.35
	cam_attr.dof_blur_far_transition = dist * 0.9
	cam_attr.dof_blur_near_enabled = dist < 40.0
	cam_attr.dof_blur_near_distance = dist * 0.55
	cam_attr.dof_blur_near_transition = dist * 0.3
	cam_attr.dof_blur_amount = 0.05
	env.ssao_radius = clampf(dist * 0.012, 0.12, 1.2)
	# territory reads as tint only when zoomed out; up close the border lines carry it
	mats.terrain.set_shader_parameter("territory_strength", smoothstep(7.0, 60.0, dist) * 1.5 + smoothstep(60.0, 150.0, dist) * 0.9)
	mats.border.set_shader_parameter("glow_strength", (0.45 + 0.55 * smoothstep(6.0, 40.0, dist)) * (0.45 if golden else 1.0))
	var far := dist > 70.0
	if world_root:
		var bn := world_root.get_node_or_null("Borders")
		var bf := world_root.get_node_or_null("BordersFar")
		if bn and bf:
			bn.visible = borders_on and not far
			bf.visible = borders_on and far
	if hud:
		hud.text = "%s  year %d  season %s%s   |  drag orbit · wheel zoom · WASD pan · 1-4 season · 5/6 frame · T golden hour" % [
			current_frame, world.year, ["spring", "summer", "autumn", "winter"][season], "  golden" if golden else ""]

func _unhandled_input(e: InputEvent) -> void:
	if e is InputEventMouseButton:
		if e.button_index == MOUSE_BUTTON_WHEEL_UP and e.pressed:
			dist = maxf(2.0, dist * 0.9)
		elif e.button_index == MOUSE_BUTTON_WHEEL_DOWN and e.pressed:
			dist = minf(260.0, dist * 1.1)
		elif e.button_index in [MOUSE_BUTTON_LEFT, MOUSE_BUTTON_RIGHT, MOUSE_BUTTON_MIDDLE]:
			dragging = e.pressed
		_update_camera()
	elif e is InputEventMouseMotion and dragging:
		yaw -= e.relative.x * 0.006
		pitch = clampf(pitch + e.relative.y * 0.004, deg_to_rad(15.0), deg_to_rad(85.0))
		_update_camera()
	elif e is InputEventKey and e.pressed and not e.echo:
		match e.keycode:
			KEY_1: _apply_season(0)
			KEY_2: _apply_season(1)
			KEY_3: _apply_season(2)
			KEY_4: _apply_season(3)
			KEY_5:
				_load_frame("frame20")
				_apply_lighting()
			KEY_6:
				_load_frame("frame40")
				_apply_lighting()
			KEY_T:
				golden = not golden
				_apply_lighting()
			KEY_B:
				borders_on = not borders_on
				_update_camera()
			KEY_F: _frame_settlement(capital_index(), 6.0, 40.0)

func _process(delta: float) -> void:
	var mv := Vector2.ZERO
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP): mv.y -= 1
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN): mv.y += 1
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT): mv.x -= 1
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT): mv.x += 1
	var rot := 0.0
	if Input.is_key_pressed(KEY_Q): rot += 1
	if Input.is_key_pressed(KEY_E): rot -= 1
	if mv != Vector2.ZERO or rot != 0.0:
		var f := Vector3(-sin(yaw), 0, -cos(yaw))
		var r := Vector3(cos(yaw), 0, -sin(yaw))
		target += (r * mv.x - f * mv.y) * dist * 0.6 * delta
		target.y = maxf(world.height_at(Vector2(target.x, target.z)), 0.0)
		yaw += rot * delta * 1.2
		_update_camera()

# ------------------------------------------------------------------ shot mode

func _wait(frames: int) -> void:
	for i in frames:
		await RenderingServer.frame_post_draw

func _save(path: String) -> void:
	await _wait(20)
	var img := get_viewport().get_texture().get_image()
	if img.get_width() != 1600:
		img.resize(1600, 900, Image.INTERPOLATE_LANCZOS)
	img.save_png(path)
	print("saved ", path)

func _set_view(tx: float, tz: float, d: float, pitch_deg: float, yaw_deg: float = 0.0) -> void:
	target = Vector3(tx, maxf(world.height_at(Vector2(tx, tz)), 0.0), tz)
	dist = d
	pitch = deg_to_rad(pitch_deg)
	yaw = deg_to_rad(yaw_deg)
	_update_camera()

func _shoot() -> void:
	var out := ProjectSettings.globalize_path("res://shots")
	DirAccess.make_dir_recursive_absolute(out)
	var only := ""
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--only="):
			only = a.substr(7)
	hud.visible = false
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--frame="):
			_load_frame(a.substr(8))
	var home: Vector3 = builder.hearths[0]
	var cap: Vector3 = builder.hearths[capital_index()]
	var shots := [
		["map_scale", func(): _apply_season(world.season); golden = false; _apply_lighting(); _set_view(home.x + 1.0, home.z - 4.0, 40.0, 45.0, 62.0)],
		["settlement_close", func(): _apply_season(world.season); golden = false; _apply_lighting(); _set_view(cap.x + 0.3, cap.z + 0.2, 5.0, 40.0, 30.0)],
		["region_overview", func(): _apply_season(world.season); golden = false; _apply_lighting(); _set_view(80.0, 45.0, 150.0, 62.0)],
		["season_spring", func(): golden = false; _apply_season(0); _set_view(home.x + 1.0, home.z - 4.0, 20.0, 42.0, 8.0)],
		["season_summer", func(): golden = false; _apply_season(1); _set_view(home.x + 1.0, home.z - 4.0, 20.0, 42.0, 8.0)],
		["season_autumn", func(): golden = false; _apply_season(2); _set_view(home.x + 1.0, home.z - 4.0, 20.0, 42.0, 8.0)],
		["season_winter", func(): golden = false; _apply_season(3); _set_view(home.x + 1.0, home.z - 4.0, 20.0, 42.0, 8.0)],
		["hearth_zoom", func(): _apply_season(world.season); golden = false; _apply_lighting(); _set_view(cap.x, cap.z, 2.2, 34.0, 25.0)],
		["coast_zoom", func(): _apply_season(world.season); golden = false; _apply_lighting(); _set_view(61.0, 28.0, 7.0, 40.0, 30.0)],
		["dawn_dusk", func(): _apply_season(world.season); golden = true; _apply_lighting(); _set_view(cap.x + 0.2, cap.z + 0.2, 8.0, 32.0, 20.0)],
	]
	for s in shots:
		if only != "" and not (s[0] in only.split(",")):
			continue
		if only == "" and (s[0] == "hearth_zoom" or s[0] == "coast_zoom"):
			continue
		s[1].call()
		await _save(out.path_join(s[0] + ".png"))
	await _measure_perf(out)
	get_tree().quit()

## The largest settlement of civ 0 (the player's capital in these shots).
func capital_index() -> int:
	var best := 0
	var bp := -1
	for i in world.settlements.size():
		var st: Dictionary = world.settlements[i]
		if int(st.civ) == 0 and int(st.population) > bp:
			bp = int(st.population)
			best = i
	return best

func _measure_perf(out: String) -> void:
	golden = false
	_apply_season(world.season)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	var ua := OS.get_cmdline_user_args()
	if "--noshadow" in ua:
		sun.shadow_enabled = false
	if "--nopost" in ua:
		env.ssao_enabled = false
		env.glow_enabled = false
	if "--noprops" in ua:
		world_root.get_node("Props").visible = false
	var home: Vector3 = builder.hearths[0]
	var cap: Vector3 = builder.hearths[capital_index()]
	var views := [
		["map_scale (40 tiles across)", Vector3(home.x + 1.0, home.z - 4.0, 40.0), 45.0, 62.0],
		["settlement_close (6 tiles)", Vector3(cap.x + 0.3, cap.z + 0.2, 5.0), 40.0, 30.0],
		["region_overview (whole map)", Vector3(80.0, 45.0, 150.0), 62.0, 0.0],
	]
	var txt := "Frame time, 1600x900, vsync off, all effects on (MSAA 4x + FXAA, SSAO, soft PCF shadows, glow, DOF); about 16.7 ms is the display cap\n"
	txt += "Machine: %s / %s\n" % [OS.get_name(), RenderingServer.get_video_adapter_name()]
	for v in views:
		var p: Vector3 = v[1]
		_set_view(p.x, p.y, p.z, v[2], v[3])
		if "--nopost" in ua:
			cam_attr.dof_blur_far_enabled = false
			cam_attr.dof_blur_near_enabled = false
		await _wait(40)
		var t0 := Time.get_ticks_usec()
		var n := 150
		for i in n:
			await RenderingServer.frame_post_draw
		var ms := float(Time.get_ticks_usec() - t0) / 1000.0 / n
		var prims := Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME)
		var draws := Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)
		txt += "%-30s %6.2f ms (%3.0f fps)  primitives %8d  draw calls %4d\n" % [v[0], ms, 1000.0 / ms, prims, draws]
	txt += "build stats %s\n" % str(builder.stats)
	print(txt)
	var f := FileAccess.open(out.path_join("perf.txt"), FileAccess.WRITE)
	f.store_string(txt)
