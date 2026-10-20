## CivSim painted-map prototype: draws a real match frame as an illustrated tabletop map.
## Mouse: left-drag orbit/tilt, right-drag tilt, wheel zoom. WASD/arrows pan, Q/E rotate.
## Keys: 1-4 season, 5/6 frame20/frame40, G hex overlay, H golden hour, T tilt-shift, L labels, P save a screenshot.
## Run with `-- --shot` to render the standard shots into shots/ and quit.
extends Node3D

const FrameData = preload("res://scripts/frame_data.gd")
const TerrainBake = preload("res://scripts/terrain_bake.gd")
const Palette = preload("res://scripts/palette.gd")
const OrbitCamera = preload("res://scripts/orbit_camera.gd")
const Geo = preload("res://scripts/geo.gd")
const Lines = preload("res://scripts/lines.gd")
const Instancer = preload("res://scripts/instancer.gd")
const Vegetation = preload("res://scripts/vegetation.gd")
const Settlements = preload("res://scripts/settlements.gd")
const Labels = preload("res://scripts/labels.gd")

const DATA_DIR := "../data/"

var fd: FrameData
var bake: TerrainBake
var season := 0
var frame_name := "frame40"
var golden := false

var cam: OrbitCamera
var sun: DirectionalLight3D
var env: WorldEnvironment
var terrain_mat: ShaderMaterial
var post_mat: ShaderMaterial
var line_mats: Array[ShaderMaterial] = []
var fx_mats: Array[ShaderMaterial] = []
var flag_nodes: Array[Node3D] = []
var fire_lights: Array[OmniLight3D] = []
var inst: Instancer
var world_root: Node3D
var labels: Labels
var paper_tex: Texture2D
var time_s := 0.0
var shot_mode := false
var frame_times: Array[float] = []
var tilt_on := 1.0


func _ready() -> void:
	shot_mode = "--shot" in OS.get_cmdline_user_args()
	paper_tex = _make_paper()
	_setup_env()
	cam = OrbitCamera.new()
	add_child(cam)
	_setup_post()
	var layer := CanvasLayer.new()
	add_child(layer)
	labels = Labels.new()
	labels.cam = cam
	layer.add_child(labels)
	cam.ground_fn = func(p: Vector2) -> float: return bake.height_at(p) if bake else 0.0
	load_frame(frame_name)
	view_preset("map")
	if shot_mode:
		get_tree().create_timer(240.0).timeout.connect(get_tree().quit)
		await _run_shots()
		get_tree().quit()


func _data_path(n: String) -> String:
	return ProjectSettings.globalize_path("res://").path_join(DATA_DIR + n + ".json").simplify_path()


# ------------------------------------------------------------------ building the world

func load_frame(n: String) -> void:
	frame_name = n
	var t0 := Time.get_ticks_msec()
	fd = FrameData.load_file(_data_path(n))
	season = fd.season
	if world_root:
		world_root.queue_free()
	world_root = Node3D.new()
	add_child(world_root)
	line_mats.clear()
	fx_mats.clear()
	flag_nodes.clear()
	fire_lights.clear()
	var rivers := Geo.rivers(fd)
	for r in rivers:
		var pts: PackedVector2Array = r.pts
		var ws: PackedFloat32Array = r.widths
		var cut := 0
		for s in fd.settlements:
			for i in pts.size():
				if pts[i].distance_to(s.pos) < 0.5:
					if i == cut:
						cut = i + 1
		if cut > 0 and cut < pts.size() - 2:
			r.pts = pts.slice(cut)
			ws = ws.slice(cut)
			for i in mini(10, ws.size()):
				ws[i] *= 0.3 + 0.07 * i
			r.widths = ws
	var spots := PackedFloat32Array()
	for s in fd.settlements:
		spots.append_array([s.pos.x, s.pos.y, 0.78 + 0.004 * s.population, 1.0])
	for r in Settlements.reserved(fd):
		spots.append_array([r.x, r.y, r.z, 0.35])
	for c in fd.camps:
		spots.append_array([c.pos.x, c.pos.y, 0.3, 0.6])
	bake = TerrainBake.new()
	bake.bake(fd, Geo.segments(rivers), spots)
	_build_terrain()
	_build_lines(rivers)
	inst = Instancer.new()
	inst.paper_tex = paper_tex
	Vegetation.place(fd, bake, inst)
	var fx := Settlements.build(fd, bake, inst)
	var np := Settlements.people(fd, bake, inst)
	var na := Settlements.herds(fd, bake, inst)
	inst.build(world_root)
	_build_fx(fx)
	labels.set_settlements(fd, bake)
	apply_season()
	print("frame %s (year %d): %d figures, %d animals; built in %d ms" % [n, fd.year, np, na, Time.get_ticks_msec() - t0])


func _make_paper() -> Texture2D:
	var n := FastNoiseLite.new()
	n.noise_type = FastNoiseLite.TYPE_CELLULAR
	n.frequency = 0.06
	n.fractal_type = FastNoiseLite.FRACTAL_FBM
	n.fractal_octaves = 3
	n.cellular_return_type = FastNoiseLite.RETURN_DISTANCE2_SUB
	var img := n.get_seamless_image(512, 512)
	img.generate_mipmaps()
	return ImageTexture.create_from_image(img)


func _setup_env() -> void:
	env = WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_COLOR
	e.background_color = Palette.PAPER.darkened(0.1)
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	env.environment = e
	add_child(env)
	sun = DirectionalLight3D.new()
	sun.shadow_enabled = true
	sun.shadow_blur = 2.0
	sun.directional_shadow_max_distance = 70.0
	add_child(sun)
	set_golden(false)


func set_golden(on: bool) -> void:
	golden = on
	var e := env.environment
	if on:
		sun.rotation_degrees = Vector3(-13, -72, 0)
		sun.light_color = Color("#ffcf92")
		sun.light_energy = 2.3
		sun.shadow_opacity = 0.55
		e.ambient_light_color = Color("#b3aed6")
		e.ambient_light_energy = 0.55
	else:
		sun.rotation_degrees = Vector3(-36, -66, 0)
		sun.light_color = Color("#fff1dc")
		sun.light_energy = 1.5
		sun.shadow_opacity = 0.5
		e.ambient_light_color = Color("#a7aec4")
		e.ambient_light_energy = 0.42
	if post_mat:
		post_mat.set_shader_parameter("grade_light", Vector3(1.1, 0.97, 0.84) if on else Vector3(1.03, 1.0, 0.94))
		post_mat.set_shader_parameter("grade_shadow", Vector3(0.95, 0.92, 1.06) if on else Vector3(0.98, 0.95, 1.04))
	for m in line_mats:
		m.set_shader_parameter("light_level", 0.8 if on else 1.0)
	if terrain_mat:
		terrain_mat.set_shader_parameter("sun_dir", sun.global_transform.basis.z)
		terrain_mat.set_shader_parameter("light_wrap", 0.35 if on else 0.0)
	for l in fire_lights:
		l.light_energy = 0.75 if on else 0.3


func _setup_post() -> void:
	var q := MeshInstance3D.new()
	var qm := QuadMesh.new()
	qm.size = Vector2(2, 2)
	q.mesh = qm
	q.extra_cull_margin = 16384.0
	q.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	post_mat = ShaderMaterial.new()
	post_mat.shader = load("res://shaders/post.gdshader")
	post_mat.set_shader_parameter("paper_tex", paper_tex)
	post_mat.set_shader_parameter("ink", _lin(Palette.INK))
	post_mat.set_shader_parameter("paper", _lin(Palette.PAPER))
	post_mat.render_priority = -100
	q.material_override = post_mat
	cam.add_child(q)


func _build_terrain() -> void:
	var terrain := MeshInstance3D.new()
	var pm := PlaneMesh.new()
	var size := Vector2((bake.bw - 1) / TerrainBake.RES, (bake.bh - 1) / TerrainBake.RES)
	pm.size = size
	pm.subdivide_width = bake.bw - 2
	pm.subdivide_depth = bake.bh - 2
	terrain.mesh = pm
	terrain.position = Vector3(-TerrainBake.MARGIN + size.x * 0.5, 0, -TerrainBake.MARGIN + size.y * 0.5)
	terrain.custom_aabb = AABB(Vector3(-size.x * 0.5, -1, -size.y * 0.5), Vector3(size.x, 4, size.y))
	terrain_mat = ShaderMaterial.new()
	terrain_mat.shader = load("res://shaders/terrain.gdshader")
	terrain_mat.set_shader_parameter("bake_tex", bake.texture)
	terrain_mat.set_shader_parameter("res", TerrainBake.RES)
	terrain_mat.set_shader_parameter("margin", TerrainBake.MARGIN)
	terrain_mat.set_shader_parameter("bake_size", Vector2(bake.bw, bake.bh))
	terrain_mat.set_shader_parameter("map_tiles", Vector2i(fd.width, fd.height))
	terrain_mat.set_shader_parameter("paper_tex", paper_tex)
	terrain_mat.set_shader_parameter("paper", _lin(Palette.PAPER))
	terrain_mat.set_shader_parameter("ink", _lin(Palette.INK))
	terrain_mat.set_shader_parameter("water_shallow", _lin(Color("#9cc3c0")))
	terrain_mat.set_shader_parameter("water_deep", _lin(Color("#6e9bb0")))
	terrain_mat.set_shader_parameter("rock_col", _lin(Color("#8c7c68")))
	terrain_mat.set_shader_parameter("rock_col2", _lin(Color("#a49279")))
	terrain_mat.set_shader_parameter("earth_col", _lin(Color("#a08454")))
	terrain_mat.set_shader_parameter("sun_dir", sun.global_transform.basis.z)
	terrain_mat.set_shader_parameter("world_size", fd.world_size())
	terrain_mat.set_shader_parameter("light_wrap", 0.35 if golden else 0.0)
	var civs := PackedColorArray()
	for c in Palette.CIV:
		civs.append(c.srgb_to_linear())
	terrain_mat.set_shader_parameter("civ_col", civs)
	var n := fd.width * fd.height
	var a := PackedByteArray()
	var b := PackedByteArray()
	a.resize(n * 4)
	b.resize(n * 4)
	for t in n:
		a[t * 4] = fd.biome[t]
		a[t * 4 + 1] = fd.elevation[t]
		a[t * 4 + 2] = fd.owner[t] + 1
		a[t * 4 + 3] = fd.claim[t]
		b[t * 4] = fd.flags[t] & 255
		b[t * 4 + 1] = fd.plot_kind[t] + 1
		b[t * 4 + 2] = 0
		b[t * 4 + 3] = ((t * 2654435761) >> 24) & 255
	var ia := Image.create_from_data(fd.width, fd.height, false, Image.FORMAT_RGBA8, a)
	var ib := Image.create_from_data(fd.width, fd.height, false, Image.FORMAT_RGBA8, b)
	terrain_mat.set_shader_parameter("tile_tex", ImageTexture.create_from_image(ia))
	terrain_mat.set_shader_parameter("tile_tex2", ImageTexture.create_from_image(ib))
	terrain.material_override = terrain_mat
	world_root.add_child(terrain)


func _build_lines(rivers: Array) -> void:
	var line_shader: Shader = load("res://shaders/line.gdshader")
	var rm := Lines.ribbon_mesh(rivers, bake, 0.012, false)
	if rm:
		var mi := MeshInstance3D.new()
		mi.mesh = rm
		var m := ShaderMaterial.new()
		m.shader = line_shader
		m.set_shader_parameter("mode", 0)
		m.set_shader_parameter("min_px", 1.8)
		m.set_shader_parameter("ink", _lin(Palette.INK))
		m.set_shader_parameter("water", _lin(Color("#86aebb")))
		mi.material_override = m
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		world_root.add_child(mi)
		line_mats.append(m)
	var polys := []
	for bl in Geo.borders(fd, 0.05):
		var col: Color = Palette.CIV[bl.civ].srgb_to_linear()
		col.a = float(bl.civ % 4) / 8.0
		polys.append({"pts": bl.pts, "color": col, "widths": _const_widths(bl.pts.size(), 0.032)})
	var bm := Lines.ribbon_mesh(polys, bake, 0.016, true)
	if bm:
		var mi2 := MeshInstance3D.new()
		mi2.mesh = bm
		var m2 := ShaderMaterial.new()
		m2.shader = line_shader
		m2.set_shader_parameter("mode", 1)
		m2.set_shader_parameter("min_px", 3.4)
		m2.set_shader_parameter("ink", _lin(Palette.INK))
		mi2.material_override = m2
		mi2.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		world_root.add_child(mi2)
		line_mats.append(m2)


static func _const_widths(n: int, w: float) -> PackedFloat32Array:
	var a := PackedFloat32Array()
	a.resize(n)
	a.fill(w)
	return a


func _build_fx(fx: Dictionary) -> void:
	var fire_shader: Shader = load("res://shaders/fire.gdshader")
	var smoke_shader: Shader = load("res://shaders/smoke.gdshader")
	var flag_shader: Shader = load("res://shaders/flag.gdshader")
	var quad := QuadMesh.new()
	quad.size = Vector2(1, 1)
	quad.center_offset = Vector3(0, 0.5, 0)
	var i := 0
	for f in fx.fires:
		i += 1
		var size: float = f.size
		var fm := ShaderMaterial.new()
		fm.shader = fire_shader
		fm.set_shader_parameter("seed", i * 1.37)
		fm.set_shader_parameter("ink", _lin(Palette.INK))
		var fire := MeshInstance3D.new()
		fire.mesh = quad
		fire.material_override = fm
		fire.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		fire.position = f.pos + Vector3(0, 0.003, 0)
		fire.scale = Vector3.ONE * 0.085 * size
		fire.visibility_range_end = 60.0
		world_root.add_child(fire)
		fx_mats.append(fm)
		var sm := ShaderMaterial.new()
		sm.shader = smoke_shader
		sm.set_shader_parameter("seed", i * 0.31)
		sm.render_priority = 1
		var smoke := MeshInstance3D.new()
		smoke.mesh = quad
		smoke.material_override = sm
		smoke.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		smoke.position = f.pos + Vector3(0, 0.05, 0)
		smoke.scale = Vector3.ONE * 0.5 * size
		smoke.visibility_range_end = 70.0
		world_root.add_child(smoke)
		fx_mats.append(sm)
		var l := OmniLight3D.new()
		l.light_color = Color("#ffb066")
		l.light_energy = 0.3
		l.omni_range = 0.75 * size
		l.omni_attenuation = 0.7
		l.position = f.pos + Vector3(0, 0.28, 0)
		l.distance_fade_enabled = true
		l.distance_fade_begin = 40.0
		l.distance_fade_length = 10.0
		world_root.add_child(l)
		fire_lights.append(l)
	var flag_mesh := PlaneMesh.new()
	flag_mesh.size = Vector2(0.15, 0.095)
	flag_mesh.orientation = PlaneMesh.FACE_Z
	flag_mesh.subdivide_width = 10
	flag_mesh.subdivide_depth = 3
	flag_mesh.center_offset = Vector3(0.075, 0.245, 0)
	var flag_mats := {}
	for fl in fx.flags:
		var civ: int = fl.civ
		if not flag_mats.has(civ):
			var m := ShaderMaterial.new()
			m.shader = flag_shader
			m.set_shader_parameter("civ_col", _lin(Palette.CIV[civ % 8]))
			m.set_shader_parameter("emblem", civ % 8)
			m.set_shader_parameter("ink", _lin(Palette.INK))
			m.set_shader_parameter("cream", _lin(Color("#f3ead6")))
			flag_mats[civ] = m
			fx_mats.append(m)
		var node := MeshInstance3D.new()
		node.mesh = flag_mesh
		node.material_override = flag_mats[civ]
		node.position = fl.pos
		node.scale = Vector3.ONE * fl.scale * 0.85
		node.set_meta("base_scale", fl.scale * 0.85)
		node.visibility_range_end = 120.0
		world_root.add_child(node)
		flag_nodes.append(node)


static func _lin(c: Color) -> Vector3:
	var l := c.srgb_to_linear()
	return Vector3(l.r, l.g, l.b)


func apply_season() -> void:
	terrain_mat.set_shader_parameter("biome_col", Palette.biome_colors(season, false))
	terrain_mat.set_shader_parameter("biome_col2", Palette.biome_colors(season, true))
	terrain_mat.set_shader_parameter("biome_snow", PackedFloat32Array(Palette.SNOW))
	terrain_mat.set_shader_parameter("snow_amount", 1.0 if season == 3 else 0.0)
	terrain_mat.set_shader_parameter("snow_line", [0.62, 0.72, 0.66, 0.42][season])
	var f := Palette.fields(season)
	terrain_mat.set_shader_parameter("field_col_a", f[0].srgb_to_linear())
	terrain_mat.set_shader_parameter("field_col_b", f[1].srgb_to_linear())
	terrain_mat.set_shader_parameter("field_col_c", f[2].srgb_to_linear())
	var cc := Palette.canopy(season)
	var keys := ["broadleaf", "conifer", "tropical", "acacia", "shrub"]
	for i in keys.size():
		inst.set_param(keys[i], "tint_color", _lin(cc[i]))
	inst.set_param("fruit", "tint_color", _lin([Color("#9fb86e"), Color("#7f9f4f"), Color("#c9763f"), Color("#8a7a6c")][season]))
	inst.set_param("rock", "tint_color", _lin(Color("#a39b8e")))
	inst.set_param("peak", "tint_color", _lin(Color("#b8a98f")))
	inst.set_param("peak", "peak_snow", [0.95, 1.12, 1.02, 0.62][season])
	inst.set_all("snow_amount", 1.0 if season == 3 else 0.0)


# ------------------------------------------------------------------ per frame

func _process(delta: float) -> void:
	time_s += delta
	if terrain_mat:
		terrain_mat.set_shader_parameter("time_s", time_s)
		# territory washes and borders carry more weight as the camera pulls back
		var zf := clampf((cam.distance - 20.0) / 80.0, 0.0, 1.0)
		terrain_mat.set_shader_parameter("territory_strength", lerpf(0.75, 1.7, zf))
		terrain_mat.set_shader_parameter("hachure_strength", 1.0 - zf * 0.6)
		post_mat.set_shader_parameter("tilt_shift", tilt_on * (1.0 - zf * 0.85))
		post_mat.set_shader_parameter("hatch", 1.0 - zf)
		var wpp := cam.distance * 2.0 * tan(deg_to_rad(cam.fov) * 0.5) / get_viewport().get_visible_rect().size.y
		for lm in line_mats:
			lm.set_shader_parameter("world_per_px", wpp)
			lm.set_shader_parameter("cam_dist", cam.distance)
		if line_mats.size() > 1:
			line_mats[1].set_shader_parameter("min_px", lerpf(3.2, 6.0, zf))
	if inst:
		inst.set_time(time_s)
	for m in fx_mats:
		m.set_shader_parameter("time_s", time_s)
	# banners grow as the camera pulls back so civ identity still reads at map scale
	var fz := lerpf(1.0, 2.4, smoothstep(8.0, 60.0, cam.distance))
	for f in flag_nodes:
		f.rotation.y = cam.yaw
		f.scale = Vector3.ONE * f.get_meta("base_scale", 1.0) * fz
	frame_times.append(delta)
	if frame_times.size() > 240:
		frame_times.pop_front()


func _unhandled_key_input(event: InputEvent) -> void:
	var k := event as InputEventKey
	if not k.pressed or k.echo:
		return
	match k.keycode:
		KEY_1, KEY_2, KEY_3, KEY_4:
			season = k.keycode - KEY_1
			apply_season()
		KEY_5:
			load_frame("frame20")
		KEY_6:
			load_frame("frame40")
		KEY_G:
			var g: float = terrain_mat.get_shader_parameter("grid_on")
			terrain_mat.set_shader_parameter("grid_on", 1.0 - g)
		KEY_H:
			set_golden(not golden)
		KEY_T:
			tilt_on = 1.0 - tilt_on
		KEY_L:
			labels.visible = not labels.visible
		KEY_P:
			var img := get_viewport().get_texture().get_image()
			var path := ProjectSettings.globalize_path("res://shots/capture_%d.png" % Time.get_unix_time_from_system())
			img.save_png(path)
			print("saved ", path)


# ------------------------------------------------------------------ shots

func _settlement_world(i: int) -> Vector3:
	var s: Dictionary = fd.settlements[i]
	return Vector3(s.pos.x, bake.height_at(s.pos), s.pos.y)


func view_preset(name: String) -> void:
	match name:
		"map":
			var t := _settlement_world(0)
			cam.set_view(t + Vector3(2.5, 0, -3.5), 40.0, 0.0, 50.0)
		"close":
			var t := _settlement_world(0)
			cam.set_view(t + Vector3(0.05, 0.05, 0.05), 3.4, -18.0, 38.0)
		"region":
			cam.set_view(Vector3(80.25, 0.0, 44.0), 150.0, 0.0, 66.0)
		"season":
			var t := _settlement_world(7)
			cam.set_view(t + Vector3(-1.0, 0, -1.0), 15.0, 10.0, 46.0)
		"golden":
			var t := _settlement_world(7)
			cam.set_view(t + Vector3(0.6, 0.0, -0.3), 9.5, -24.0, 33.0)


func _shot(path: String) -> void:
	for i in 6:
		await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	img.save_png(ProjectSettings.globalize_path("res://shots/" + path))
	print("saved %s (%dx%d)" % [path, img.get_width(), img.get_height()])


func _measure(label: String) -> void:
	var vp := get_viewport().get_viewport_rid()
	RenderingServer.viewport_set_measure_render_time(vp, true)
	for i in 30:
		await RenderingServer.frame_post_draw
	var t0 := Time.get_ticks_usec()
	var gpu := 0.0
	var cpu := 0.0
	var n := 120
	for i in n:
		await RenderingServer.frame_post_draw
		gpu += RenderingServer.viewport_get_measured_render_time_gpu(vp)
		cpu += RenderingServer.viewport_get_measured_render_time_cpu(vp)
	var wall := (Time.get_ticks_usec() - t0) / 1000.0 / n
	print("perf %s: %.2f ms/frame wall (%.0f fps), gpu %.2f ms, render cpu %.2f ms, %d draw calls, %d primitives" % [
		label, wall, 1000.0 / wall, gpu / n, cpu / n,
		RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME),
		RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME)])


func _run_shots() -> void:
	var only := ""
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--only="):
			only = a.substr(7)
	var want := func(n: String) -> bool: return only == "" or n in only.split(",")
	if only.contains("grid_check"):
		view_preset("map")
		terrain_mat.set_shader_parameter("grid_on", 1.0)
		await _shot("grid_check.png")
		terrain_mat.set_shader_parameter("grid_on", 0.0)
	if only.contains("frame20_check"):
		load_frame("frame20")
		view_preset("map")
		await _shot("frame20_check.png")
		load_frame("frame40")
	if want.call("map_scale"):
		view_preset("map")
		await _shot("map_scale.png")
		await _measure("map_scale")
	if want.call("settlement_close"):
		view_preset("close")
		await _shot("settlement_close.png")
		await _measure("settlement_close")
	if want.call("region_overview"):
		view_preset("region")
		await _shot("region_overview.png")
		await _measure("region_overview")
	for si in 4:
		var n: String = "season_" + Palette.SEASON_NAMES[si]
		if want.call(n):
			season = si
			apply_season()
			view_preset("season")
			await _shot(n + ".png")
	season = fd.season
	apply_season()
	if want.call("golden_hour"):
		set_golden(true)
		view_preset("golden")
		await _shot("golden_hour.png")
		set_golden(false)
