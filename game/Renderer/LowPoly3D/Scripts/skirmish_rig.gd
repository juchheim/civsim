## Raid skirmish: when a raid party reaches the settlement it struck, the host (Game.Skirmish.cs) plays a short fight on the map
## before the result toast. Raiders run in from the edge of the hex on the side they came from, the defenders come out from the
## hearth, the two lines clash in rolling dust with small sparks, then the losing side breaks (some fall, the rest run off and
## fade) while the winners stand, hop and raise their arms under a banner and a ring of light in the winning civilization's colour.
##
## Purely cosmetic and played in real time (it runs while paused and at any speed); nothing is read back by the simulation. The
## fighters are the ordinary people meshes (person_spear, person_feast_cheer, shadow_disc) with the figure material, so they wear
## the same civ mantles as the people the player has watched walking. Shaders and meshes are built once and shared by every
## skirmish; each fight owns only its MultiMesh buffers, freed when it ends.
extends RefCounted

const SIDE_MAX := 6
const ASSAULT_SIDE_MAX := 8
const MAX_PUFFS := 48
const MAX_SPARKS := 10

# timeline in seconds (normal / reduced motion)
const T_FULL := {"rush": 1.0, "brk": 2.15, "victory": 2.55, "fade": 3.75, "end": 4.3}
const T_CALM := {"rush": 0.9, "brk": 1.7, "victory": 2.0, "fade": 3.0, "end": 3.5}
const ASSAULT_EXTRA := 0.5             # an assault fights half a second longer

const DUST_SHADER := """
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform float soft_fade = 0.05;
varying float seedv;
void vertex() {
	seedv = INSTANCE_CUSTOM.r;
	float s = length(MODEL_MATRIX[0].xyz);
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * s, INV_VIEW_MATRIX[1] * s, INV_VIEW_MATRIX[2] * s, MODEL_MATRIX[3]);
}
void fragment() {
	vec2 q = UV - 0.5;
	float ang = atan(q.y, q.x);
	float lobes = 0.09 * sin(ang * 5.0 + seedv * 41.0) + 0.06 * sin(ang * 3.0 - seedv * 23.0) + 0.04 * sin(ang * 8.0 + seedv * 7.0);
	float d = length(q) * 2.0 / (1.0 + lobes);
	float a = 1.0 - smoothstep(0.5, 1.0, d);
	// a soft lit top and a shaded underside so the puff reads as a round billow, not a disc
	float shade = mix(1.22, 0.6, clamp(UV.y * 1.15 + d * 0.1, 0.0, 1.0));
	float depth = textureLod(depth_tex, SCREEN_UV, 0.0).r;
	vec4 scene = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
	scene.xyz /= scene.w;
	float soft = clamp((VERTEX.z - scene.z) / soft_fade, 0.0, 1.0);
	ALBEDO = COLOR.rgb * shade;
	ALPHA = a * COLOR.a * soft;
}
"""

const SPARK_SHADER := """
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
void vertex() {
	float s = length(MODEL_MATRIX[0].xyz);
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * s, INV_VIEW_MATRIX[1] * s, INV_VIEW_MATRIX[2] * s, MODEL_MATRIX[3]);
}
void fragment() {
	vec2 q = UV - 0.5;
	float d = length(q) * 2.0;
	float core = exp(-d * d * 14.0);
	float rays = max(0.0, 1.0 - abs(q.x) * 16.0) * max(0.0, 1.0 - abs(q.y) * 2.1)
		+ max(0.0, 1.0 - abs(q.y) * 16.0) * max(0.0, 1.0 - abs(q.x) * 2.1);
	ALBEDO = COLOR.rgb;
	ALPHA = clamp((core + rays * 0.7) * COLOR.a, 0.0, 1.0);
}
"""

const RING_SHADER := """
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
varying vec4 params;
void vertex() {
	params = INSTANCE_CUSTOM;
}
void fragment() {
	float d = length(UV - 0.5) * 2.0;
	float a;
	vec3 c = COLOR.rgb;
	if (params.x < 0.5) {
		// a ring: a bright line with a soft glow on both sides
		float w = params.y;
		float band = 1.0 - smoothstep(0.0, w, abs(d - 0.86));
		float line = 1.0 - smoothstep(0.0, w * 0.3, abs(d - 0.86));
		a = band * 0.7 + line * 0.5;
		c = mix(c, vec3(1.0), line * 0.45);
	} else {
		// a glow pooled on the ground under the winners
		float g = clamp(1.0 - d, 0.0, 1.0);
		a = g * g * (3.0 - 2.0 * g);
	}
	ALBEDO = c;
	ALPHA = clamp(a, 0.0, 1.0) * COLOR.a;
}
"""

var b                               # the M7WorldBuilder
var holder: Node3D
var fights: Array = []
var dust_mat: ShaderMaterial
var spark_mat: ShaderMaterial
var ring_mat: ShaderMaterial
var quad: QuadMesh
var plane: PlaneMesh
var next_id := 1
var cam_pos := Vector3.ZERO

func _init(builder, parent: Node3D) -> void:
	b = builder
	holder = Node3D.new()
	holder.name = "Skirmishes"
	parent.add_child(holder)

func _ensure_shared() -> void:
	if quad != null:
		return
	quad = QuadMesh.new()
	quad.size = Vector2.ONE
	plane = PlaneMesh.new()
	plane.size = Vector2(2.0, 2.0)
	for k in ["dust", "spark", "ring"]:
		var sh := Shader.new()
		sh.code = DUST_SHADER if k == "dust" else (SPARK_SHADER if k == "spark" else RING_SHADER)
		var m := ShaderMaterial.new()
		m.shader = sh
		m.render_priority = 4 if k == "ring" else (6 if k == "dust" else 7)
		if k == "dust":
			dust_mat = m
		elif k == "spark":
			spark_mat = m
		else:
			ring_mat = m

func _multimesh(parent: Node3D, mesh: Mesh, material: Material, count: int, colors: bool, custom: bool) -> MultiMesh:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_colors = colors
	mm.use_custom_data = custom
	mm.mesh = mesh
	mm.instance_count = count
	var zero := Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO)
	for i in count:
		mm.set_instance_transform(i, zero)
	var inst := MultiMeshInstance3D.new()
	inst.multimesh = mm
	inst.material_override = material
	inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	inst.extra_cull_margin = 2.0
	parent.add_child(inst)
	return mm

static func _smooth(x: float) -> float:
	var t := clampf(x, 0.0, 1.0)
	return t * t * (3.0 - 2.0 * t)

static func _shown(count: int, most: int) -> int:
	return clampi((count + 1) / 2, 2, most)

func _ground(p: Vector2) -> float:
	return maxf(b.w.height_at(p), 0.0)

func _dust_colour(tile: int) -> Color:
	var biome: int = b.w.biome[tile] if tile >= 0 and tile < b.w.biome.size() else M7WorldData.Biome.GRASSLAND
	var c := Color("#c4a274")
	match biome:
		M7WorldData.Biome.DESERT, M7WorldData.Biome.SAVANNA:
			c = Color("#d2ad78")
		M7WorldData.Biome.TEMPERATE_FOREST, M7WorldData.Biome.TAIGA, M7WorldData.Biome.TROPICAL_FOREST:
			c = Color("#a88a66")
		M7WorldData.Biome.WETLAND:
			c = Color("#9a8a6c")
		M7WorldData.Biome.TUNDRA, M7WorldData.Biome.ICE, M7WorldData.Biome.PEAK:
			c = Color("#dcdcdc")
		M7WorldData.Biome.MOUNTAIN:
			c = Color("#a59a8c")
	return c.srgb_to_linear()

func _person_mesh(civ: int, base: String) -> Mesh:
	var tailored: bool = civ >= 0 and b.w.has_look(civ, M7WorldData.LOOK_TAILORED)
	var name := base + ("_g" if tailored and b.kit.meshes.has(base + "_g") else "")
	return b.kit.get_mesh(name)

## Starts a fight; spec keys: tile, att_civ, def_civ, att_won, att_n, def_n, att_lost, def_lost, angle, calm. Returns
## [id, seconds], or [-1, 0] when it cannot play here.
func start(spec: Dictionary) -> Array:
	var w = b.w
	var tile := int(spec.get("tile", -1))
	if w == null or tile < 0 or tile >= w.width * w.height or w.is_water(tile):
		return [-1, 0.0]
	_ensure_shared()
	var calm := bool(spec.get("calm", false))
	var assault := bool(spec.get("assault", false))
	var tl: Dictionary = (T_CALM if calm else T_FULL).duplicate()
	if assault:
		for key in ["brk", "victory", "fade", "end"]:
			tl[key] = float(tl[key]) + ASSAULT_EXTRA
	var most := ASSAULT_SIDE_MAX if assault else SIDE_MAX
	# the hearth of the settlement on the tile (the tile centre when there is none) and how far its houses reach
	var home: Vector2 = w.tile_center(tile)
	for si in w.settlements.size():
		if int(w.settlements[si].tile) == tile and si < b.hearths.size():
			home = Vector2(b.hearths[si].x, b.hearths[si].z)
			break
	var village_r := 0.25
	for v in b.villages:
		if Vector2(v.x, v.y).distance_to(home) < 0.6:
			village_r = v.z
			break
	# the raiders come in from their own side; turn off the water if that side is sea
	var angle := float(spec.get("angle", 0.0))
	for k in 7:
		var a := angle + (0.0 if k == 0 else (PI / 6.0) * ((k + 1) / 2) * (1.0 if k % 2 == 1 else -1.0))
		var probe: Vector2 = home + Vector2(cos(a), sin(a)) * 0.45
		var pt: int = w.tile_at(probe)
		if pt >= 0 and not w.is_water(pt):
			angle = a
			break
	var dir := Vector2(cos(angle), sin(angle))
	var perp := Vector2(-dir.y, dir.x)
	var clash_d := clampf(village_r * 0.9 + 0.04, 0.26, 0.32)          # at the edge of the houses, out of the hearth light
	var att_civ := int(spec.get("att_civ", 0))
	var def_civ := int(spec.get("def_civ", 1))
	var att_won := bool(spec.get("att_won", true))
	var counts := [_shown(int(spec.get("att_n", 4)), most), _shown(int(spec.get("def_n", 4)), most)]
	var came := [maxi(1, int(spec.get("att_n", 4))), maxi(1, int(spec.get("def_n", 4)))]
	var lost := [int(spec.get("att_lost", 0)), int(spec.get("def_lost", 0))]
	var root := Node3D.new()
	root.name = "Skirmish"
	holder.add_child(root)
	var f := {
		"id": next_id, "t": 0.0, "tl": tl, "calm": calm, "assault": assault, "most": most, "tile": tile, "root": root,
		"home": home, "dir": dir, "perp": perp, "clash": home + dir * clash_d,
		"civ": [att_civ, def_civ], "att_won": att_won, "counts": counts,
		"stand": [_multimesh(root, _person_mesh(att_civ, "person_spear"), b.mats.figure, most, false, true),
			_multimesh(root, _person_mesh(def_civ, "person_spear"), b.mats.figure, most, false, true)],
		"cheer": [_multimesh(root, b.kit.get_mesh("person_feast_cheer"), b.mats.figure, most, false, true),
			_multimesh(root, b.kit.get_mesh("person_feast_cheer"), b.mats.figure, most, false, true)],
		"shadow": _multimesh(root, b.kit.get_mesh("shadow_disc"), b.mats.shadow_disc, most * 2, false, true),
		"dust": _multimesh(root, quad, dust_mat, MAX_PUFFS, true, true),
		"sparks": _multimesh(root, quad, spark_mat, MAX_SPARKS, true, false),
		"rings": _multimesh(root, plane, ring_mat, 3, true, true),
		"banner": _multimesh(root, b.kit.get_mesh("banner%d" % ((att_civ if att_won else def_civ) % 8)), b.mats.banner, 1, false, true),
		"dust_col": _dust_colour(tile), "puffs": [], "puff_next": 0, "sparks_live": [], "spark_next": 0,
		"emit": 0.0, "spark_emit": 0.0, "rng": RandomNumberGenerator.new(), "burst": false,
	}
	(f.rng as RandomNumberGenerator).seed = hash([tile, att_civ, def_civ, next_id])
	next_id += 1
	# the fighters: side 0 the raiders, side 1 the defenders; fate 0 stands, 1 falls, 2 runs off
	var men: Array = []
	for side in 2:
		var n: int = counts[side]
		var loser: bool = (side == 0) != att_won
		var falls := clampi(int(round(float(n) * float(lost[side]) / float(came[side]))), 0, n)
		if loser:
			falls = clampi(falls, 0, n)
		else:
			falls = clampi(falls, 0, maxi(0, n / 3))
		var rng: RandomNumberGenerator = f.rng
		for i in n:
			var lateral := (float(i) - float(n - 1) * 0.5)
			var fate := 0
			if loser:
				fate = 1 if i < falls else 2
			elif i < falls:
				fate = 1
			var start_p: Vector2
			if side == 0:
				start_p = home + dir * (clash_d + 0.24 + rng.randf_range(-0.02, 0.05)) + perp * (lateral * 0.06 + rng.randf_range(-0.015, 0.015))
			else:
				start_p = home + dir * rng.randf_range(-0.04, 0.05) + perp * (lateral * 0.045 + rng.randf_range(-0.02, 0.02))
			men.append({
				"side": side, "i": i, "lateral": lateral, "fate": fate, "start": start_p,
				"ph": rng.randf() * TAU, "seed": rng.randf(), "stagger": rng.randf_range(0.0, 0.22),
				"jit": Vector2(rng.randf_range(-0.012, 0.012), rng.randf_range(-0.012, 0.012)),
				"fall_at": 0.0, "fall_dir": 1.0 if rng.randf() < 0.5 else -1.0, "last": start_p, "trail": rng.randf() * 0.1,
			})
	# losers break in a ragged wave; winners who fall go down during the clash
	for m in men:
		var loser: bool = (int(m.side) == 0) != att_won
		if int(m.fate) == 1:
			m.fall_at = (tl.brk + float(m.stagger) * 1.3) if loser else lerpf(tl.rush + 0.35, tl.brk - 0.25, float(m.seed))
		else:
			m.fall_at = tl.brk + float(m.stagger) * 1.3
	f["men"] = men
	fights.append(f)
	return [int(f.id), float(tl.end)]

func alive(id: int) -> bool:
	for f in fights:
		if int(f.id) == id:
			return true
	return false

func clear() -> void:
	for f in fights:
		(f.root as Node).queue_free()
	fights.clear()

## Where a fight is under way (the origin when none): a dev aid for scenarios.
func focus() -> Vector3:
	if fights.is_empty():
		return Vector3.ZERO
	var c: Vector2 = fights[fights.size() - 1].clash
	return Vector3(c.x, _ground(c), c.y)

func update(delta: float, fig_scale: float, ring_scale: float, camera_position: Vector3) -> void:
	cam_pos = camera_position
	for k in range(fights.size() - 1, -1, -1):
		var f: Dictionary = fights[k]
		f.t = float(f.t) + delta
		if float(f.t) >= float(f.tl.end):
			(f.root as Node).queue_free()
			fights.remove_at(k)
			continue
		_play(f, delta, fig_scale / 1.25, ring_scale)

func _play(f: Dictionary, delta: float, sp: float, rs: float) -> void:
	var t: float = f.t
	var tl: Dictionary = f.tl
	var calm: bool = f.calm
	var dir: Vector2 = f.dir
	var perp: Vector2 = f.perp
	var clash: Vector2 = f.clash
	var att_won: bool = f.att_won
	var rng: RandomNumberGenerator = f.rng
	var appear := _smooth(t / 0.3)
	var vanish := 1.0 - _smooth((t - float(tl.fade)) / (float(tl.end) - float(tl.fade)))
	var zero := Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO)
	var fight_on := t >= float(tl.rush) - 0.15 and t < float(tl.brk) + 0.3
	var winner_side := 0 if att_won else 1
	var enemy_mid := [clash - dir * 0.042 * sp, clash + dir * 0.042 * sp]   # where each side's foes stand
	var lat_step := 0.042 * sp
	for m in f.men:
		var side: int = m.side
		var i: int = m.i
		var ph: float = m.ph
		var fate: int = m.fate
		var stand: MultiMesh = f.stand[side]
		var cheer: MultiMesh = f.cheer[side]
		var toward := -dir if side == 0 else dir
		var slot: Vector2 = clash + dir * (0.042 * sp if side == 0 else -0.042 * sp) + perp * (float(m.lateral) * lat_step) + (m.jit as Vector2) * sp
		var p: Vector2
		var face := atan2(toward.x, toward.y)
		var moving := false
		var lift := 0.0
		var tilt := 0.0
		var fall_axis := Vector3.ZERO
		var grow := appear * vanish
		var use_cheer := false
		# the rush in: raiders from the edge, defenders out of the village a moment later
		var go := (float(tl.rush) - 0.05) if side == 0 else (float(tl.rush) - 0.4)
		var begin := (0.12 if side == 0 else 0.4) + float(m.stagger) * 0.5
		var u := clampf((t - begin) / maxf(go - begin, 0.05), 0.0, 1.0)
		var run := u * u * (3.0 - 2.0 * u)
		p = (m.start as Vector2).lerp(slot, run)
		if u > 0.0 and u < 1.0:
			moving = true
		if side == 0 and t < begin:
			# the raiders stride in from just beyond the edge as they appear
			p = (m.start as Vector2) + dir * 0.05 * (1.0 - appear)
		# the clash: jostle, lunge, sway
		if u >= 1.0 and t < float(m.fall_at) + 0.01:
			var k := 0.25 if calm else 1.0
			var wob := Vector2(sin(t * 5.3 + ph), cos(t * 4.1 + ph * 1.3)) * 0.012 * sp * k
			var lunge := 0.0 if calm else pow(maxf(0.0, sin(t * 8.5 + ph)), 3.0) * 0.024 * sp
			p = slot + wob + toward * lunge
			var mid: Vector2 = enemy_mid[side] + perp * (float(m.lateral) * lat_step * 0.8)
			var at := mid - p
			face = atan2(at.x, at.y) + (sin(t * 6.0 + ph) * (0.12 if calm else 0.4))
			lift = 0.0 if calm else absf(sin(t * 9.0 + ph)) * 0.004 * sp
			tilt = 0.0 if calm else sin(t * 7.0 + ph) * 0.1
		# what happens after the break
		var after := t - float(m.fall_at)
		if after > 0.0:
			var here: Vector2 = slot
			if fate == 1:
				# topples backwards (or sideways), stays down, fades with the rest
				var fall := clampf(after / (0.55 if calm else 0.38), 0.0, 1.0)
				var fall_e := fall * fall
				if not m.has("fd"):
					# fall across the view (to the camera's left or right) so a body on the ground reads as one from above
					var view := Vector2(here.x - cam_pos.x, here.y - cam_pos.z)
					view = view.normalized() if view.length() > 0.001 else dir
					m["fd"] = (Vector2(-view.y, view.x) * float(m.fall_dir) - toward * 0.35).normalized()
				var fd: Vector2 = m.fd
				p = here + fd * 0.01 * sp * fall_e
				fall_axis = Vector3.UP.cross(Vector3(fd.x, 0.0, fd.y)).normalized()
				tilt = fall_e * 1.5
				lift = 0.004 * sp * fall_e
				if not bool(m.get("dusted", false)):
					m["dusted"] = true
					_puff(f, Vector3(p.x, _ground(p) + 0.01 * sp, p.y), 0.05 * sp, 0.13 * sp, 0.9, Vector3(0, 0.03, 0))
			elif fate == 2:
				# turns and runs off the way they came, shrinking away
				var away := dir if side == 0 else (-dir + perp * (0.6 if i % 2 == 0 else -0.6)).normalized()
				var dist := after * (0.24 if calm else 0.32) * sp
				p = here + away * dist
				face = atan2(away.x, away.y)
				moving = true
				grow *= 1.0 - _smooth((after - 0.35) / 0.45)
			else:
				# the winners close up, then stand tall and cheer
				var gather := _smooth(after / 0.5)
				var tight: Vector2 = clash + perp * (float(m.lateral) * lat_step * 0.85) + (m.jit as Vector2) * sp * 0.5 + (dir * 0.05 * sp if side == 0 else -dir * 0.05 * sp)
				p = here.lerp(tight, gather)
				face = atan2(toward.x, toward.y) + sin(ph) * 0.35
				if t > float(tl.victory):
					var vt := t - float(tl.victory)
					if calm:
						use_cheer = i % 2 == 0
					else:
						use_cheer = sin(vt * 5.0 + ph) > -0.2
						lift = maxf(0.0, sin(vt * 9.0 + ph)) * 0.012 * sp
						face += sin(vt * 2.2 + ph) * 0.3
		if moving and fate != 1:
			lift += absf(sin(t * 15.0 + ph)) * (0.003 if calm else 0.007) * sp
			# little kicks of dust at the feet of runners
			m.trail = float(m.trail) - delta
			if float(m.trail) <= 0.0 and grow > 0.2:
				m.trail = (0.24 if calm else 0.1) + rng.randf() * 0.05
				_puff(f, Vector3(p.x, _ground(p) + 0.006 * sp, p.y), 0.025 * sp, 0.07 * sp, 0.75, Vector3(0, 0.02, 0))
		var y := _ground(p) + lift
		var basis := Basis(Vector3.UP, face)
		if fall_axis != Vector3.ZERO:
			basis = Basis(fall_axis, tilt) * basis
		elif tilt != 0.0:
			basis = basis * Basis(Vector3.RIGHT, tilt)
		var xf := Transform3D(basis.scaled(Vector3.ONE * grow), Vector3(p.x, y, p.y))
		var custom := Color(float(int(f.civ[side]) % 8) + float(m.seed) * 0.98, 0.0, 0.0, 0.0)
		stand.set_instance_transform(i, zero if use_cheer or grow <= 0.001 else xf)
		cheer.set_instance_transform(i, xf if use_cheer and grow > 0.001 else zero)
		stand.set_instance_custom_data(i, custom)
		cheer.set_instance_custom_data(i, custom)
		var shadow: MultiMesh = f.shadow
		var si := side * int(f.most) + i
		var sg := grow * (1.0 - clampf(lift / (0.02 * sp), 0.0, 0.4))
		shadow.set_instance_transform(si, Transform3D(Basis().scaled(Vector3(sg, 1.0, sg)), Vector3(p.x, _ground(p) + 0.001, p.y)))
		shadow.set_instance_custom_data(si, Color(0, 0, 0, 0))
	# dust: a churning cloud over the clash, a burst as one side breaks, then settling
	var rate := 0.0
	var big := 1.45 if bool(f.assault) else 1.0
	if fight_on:
		rate = (4.0 if calm else 15.0) * big
	elif t >= float(tl.brk) + 0.3 and t < float(tl.fade):
		rate = (0.6 if calm else 1.6) * big
	f.emit = float(f.emit) + rate * delta
	while float(f.emit) >= 1.0:
		f.emit = float(f.emit) - 1.0
		var off := Vector2(rng.randf_range(-1.0, 1.0), rng.randf_range(-1.0, 1.0)) * Vector2(0.05, 0.05) * sp
		var q: Vector2 = clash + perp * rng.randf_range(-0.12, 0.12) * sp * big + dir * rng.randf_range(-0.05, 0.05) * sp + off
		var drift := (q - clash).normalized() * rng.randf_range(0.03, 0.06) if q != clash else Vector2.ZERO
		_puff(f, Vector3(q.x, _ground(q) + 0.012 * sp, q.y), rng.randf_range(0.05, 0.08) * sp, rng.randf_range(0.14, 0.25) * sp,
			rng.randf_range(1.3, 2.0), Vector3(drift.x, rng.randf_range(0.025, 0.05), drift.y))
	if not bool(f.burst) and t >= float(tl.brk):
		f.burst = true
		for j in (3 if calm else (10 if bool(f.assault) else 7)):
			var a := TAU * j / 7.0 + rng.randf() * 0.5
			var q: Vector2 = clash + Vector2(cos(a), sin(a)) * 0.05 * sp
			_puff(f, Vector3(q.x, _ground(q) + 0.02 * sp, q.y), 0.09 * sp, 0.3 * sp, rng.randf_range(1.5, 2.1),
				Vector3(cos(a) * 0.06, 0.05, sin(a) * 0.06))
	# once one side breaks the dust thins out to show how it ended
	var settle := 1.0 - 0.6 * _smooth((t - float(tl.brk) - 0.35) / 0.7)
	_update_puffs(f, delta, vanish, settle)
	# sparks where spears meet
	if fight_on and t > float(tl.rush) - 0.05:
		f.spark_emit = float(f.spark_emit) + (1.5 if calm else 7.0) * delta
	while float(f.spark_emit) >= 1.0:
		f.spark_emit = float(f.spark_emit) - 1.0
		var q: Vector2 = clash + perp * rng.randf_range(-0.07, 0.07) * sp + dir * rng.randf_range(-0.015, 0.015) * sp
		var sk: Array = f.sparks_live
		var s := {"pos": Vector3(q.x, _ground(q) + rng.randf_range(0.06, 0.09) * sp, q.y), "age": 0.0,
			"life": rng.randf_range(0.14, 0.22), "size": rng.randf_range(0.07, 0.1) * sp, "slot": int(f.spark_next)}
		f.spark_next = (int(f.spark_next) + 1) % MAX_SPARKS
		for old in sk:
			if int(old.slot) == int(s.slot):
				sk.erase(old)
				break
		sk.append(s)
	_update_sparks(f, delta, calm)
	# the winner: a glow on the ground, rings spreading in their colour, and their banner planted where the fight was
	var rings: MultiMesh = f.rings
	var win_col := M7WorldData.civ_color(int(f.civ[winner_side])).lightened(0.12).srgb_to_linear()
	var gy := _ground(clash) + 0.004
	var v := t - float(tl.victory)
	if v < 0.0:
		for r in 3:
			rings.set_instance_transform(r, zero)
	else:
		var glow_a := _smooth(v / 0.4) * vanish * 0.6
		var glow_r := 0.24 * maxf(sp, 1.0) * maxf(1.0, rs * 0.6)
		rings.set_instance_transform(0, Transform3D(Basis().scaled(Vector3(glow_r, 1.0, glow_r)), Vector3(clash.x, gy, clash.y)))
		rings.set_instance_color(0, Color(win_col.r, win_col.g, win_col.b, glow_a))
		rings.set_instance_custom_data(0, Color(1, 0, 0, 0))
		for r in 2:
			var lv := v - 0.35 * r
			if lv < 0.0:
				rings.set_instance_transform(1 + r, zero)
				continue
			var radius: float
			var alpha: float
			if calm:
				radius = 0.26 * rs
				alpha = _smooth(lv / 0.5) * vanish * (0.8 if r == 0 else 0.0)
			else:
				var e := 1.0 - pow(1.0 - clampf(lv / 1.3, 0.0, 1.0), 3.0)
				radius = lerpf(0.06, 0.4, e) * rs
				alpha = (1.0 - e * e) * minf(1.0, lv / 0.08)
			rings.set_instance_transform(1 + r, Transform3D(Basis().scaled(Vector3(radius, 1.0, radius)), Vector3(clash.x, gy + 0.001 * r, clash.y)))
			rings.set_instance_color(1 + r, Color(win_col.r, win_col.g, win_col.b, alpha))
			rings.set_instance_custom_data(1 + r, Color(0, clampf(0.045 / maxf(radius, 0.01), 0.05, 0.45), 0, 0))
	var banner: MultiMesh = f.banner
	if v < 0.0:
		banner.set_instance_transform(0, zero)
	else:
		var rise := _smooth(v / 0.35)
		var pop := 1.0 + 0.12 * sin(clampf(v / 0.5, 0.0, 1.0) * PI) * (0.0 if calm else 1.0)
		var bs := 1.1 * pop * vanish
		var bp: Vector2 = clash + perp * 0.012
		var cc := M7MeshBuilder.lin(M7WorldData.civ_color(int(f.civ[winner_side])), 0.5)
		banner.set_instance_transform(0, Transform3D(Basis(Vector3.UP, -0.4).scaled(Vector3(bs, bs * rise, bs)), Vector3(bp.x, _ground(bp), bp.y)))
		banner.set_instance_custom_data(0, Color(cc.r, cc.g, cc.b, 0.5))

func _puff(f: Dictionary, pos: Vector3, size0: float, size1: float, life: float, vel: Vector3) -> void:
	var puffs: Array = f.puffs
	var slot := int(f.puff_next)
	f.puff_next = (slot + 1) % MAX_PUFFS
	for old in puffs:
		if int(old.slot) == slot:
			puffs.erase(old)
			break
	var rng: RandomNumberGenerator = f.rng
	puffs.append({"slot": slot, "pos": pos, "vel": vel, "age": 0.0, "life": life, "s0": size0, "s1": size1,
		"shade": rng.randf_range(0.9, 1.08), "seed": rng.randf()})

func _update_puffs(f: Dictionary, delta: float, vanish: float, settle: float) -> void:
	var dust: MultiMesh = f.dust
	var col: Color = f.dust_col
	var puffs: Array = f.puffs
	var zero := Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO)
	var live := {}
	for k in range(puffs.size() - 1, -1, -1):
		var p: Dictionary = puffs[k]
		p.age = float(p.age) + delta
		var u: float = float(p.age) / float(p.life)
		if u >= 1.0:
			dust.set_instance_transform(int(p.slot), zero)
			puffs.remove_at(k)
			continue
		live[int(p.slot)] = true
		# billow out fast, then slow; rise and drift, thinning as it goes
		var vel: Vector3 = p.vel
		p.vel = vel * maxf(0.0, 1.0 - 1.2 * delta)
		p.pos = (p.pos as Vector3) + vel * delta
		var grow := 1.0 - pow(1.0 - u, 2.2)
		var size := lerpf(float(p.s0), float(p.s1), grow)
		var alpha := _smooth(u / 0.1) * (1.0 - _smooth((u - 0.3) / 0.7)) * 0.95 * vanish * settle
		dust.set_instance_transform(int(p.slot), Transform3D(Basis().scaled(Vector3.ONE * size), (p.pos as Vector3) + Vector3(0, size * 0.25, 0)))
		var sh: float = p.shade
		dust.set_instance_color(int(p.slot), Color(col.r * sh, col.g * sh, col.b * sh, alpha))
		dust.set_instance_custom_data(int(p.slot), Color(float(p.seed), 0, 0, 0))

func _update_sparks(f: Dictionary, delta: float, calm: bool) -> void:
	var sparks: MultiMesh = f.sparks
	var sk: Array = f.sparks_live
	var zero := Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO)
	for k in range(sk.size() - 1, -1, -1):
		var s: Dictionary = sk[k]
		s.age = float(s.age) + delta
		var u: float = float(s.age) / float(s.life)
		if u >= 1.0:
			sparks.set_instance_transform(int(s.slot), zero)
			sk.remove_at(k)
			continue
		var flash := _smooth(u / 0.2) * (1.0 - _smooth((u - 0.2) / 0.8))
		var size: float = float(s.size) * (0.6 + 0.6 * u)
		sparks.set_instance_transform(int(s.slot), Transform3D(Basis().scaled(Vector3.ONE * size), s.pos))
		sparks.set_instance_color(int(s.slot), Color(1.0, 0.9, 0.65, flash * (0.6 if calm else 1.0)))
