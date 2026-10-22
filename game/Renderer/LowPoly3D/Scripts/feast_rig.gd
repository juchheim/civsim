## Feasting (SOC-05): once a year at midsummer each village of a civilization that knows the project holds a feast beside its houses.
##
## The simulation has no feast (the action is declared and never used); the frame only says when each civilization learned the
## project (MatchFrame.CivFeastLearned). So the feast is played from the tick: about ten guests walk out from the hearth to a long
## mat laid with bowls, jars and a roast, sit down five a side, and take turns springing up with raised arms while a host stands at
## the head with arms flung wide and a cup raised; then they walk home and the mat is cleared. The guests are a cosmetic party drawn
## with the ordinary people meshes in other poses, not taken from the village's real figures. Pure function of the render tick, so
## pause, speed and loading all work and nothing is stored. (The project text's cohesion bonus and food cost are not simulated.)
class_name M7FeastRig
extends RefCounted

const YEAR := 600.0
const START := 170.0                # ticks into the year when the feast begins (midsummer), plus a village's own offset
const SPREAD := 45.0
const LENGTH := 130.0               # a feast lasts this long (13 seconds at 1x)
const COME := 0.15                  # fraction of it spent walking out
const GO := 0.85                    # ... and when the guests start for home
const MAX_GUESTS := 10
const MAT_SCALE := 1.35
const SEAT_X := [-0.1, -0.05, 0.0, 0.05, 0.1]
const SEAT_Z := 0.078
const HOST_X := 0.2
const FIRE_X := -0.135

var b                               # the M7WorldBuilder
var holder: Node3D
var sites: Array = []
var signature := -1

func _init(builder, parent: Node3D) -> void:
	b = builder
	holder = Node3D.new()
	holder.name = "Feasts"
	parent.add_child(holder)

func _multimesh(mesh_name: String, material: Material, count: int) -> MultiMeshInstance3D:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_custom_data = true
	mm.mesh = b.kit.get_mesh(mesh_name)
	mm.instance_count = count
	mm.visible_instance_count = 0
	var inst := MultiMeshInstance3D.new()
	inst.multimesh = mm
	inst.material_override = material
	inst.extra_cull_margin = 2.5
	inst.visible = false
	holder.add_child(inst)
	return inst

static func _smooth(x: float) -> float:
	var t := clampf(x, 0.0, 1.0)
	return t * t * (3.0 - 2.0 * t)

func _tree_near(p: Vector2, radius: float) -> bool:
	for tp in b.tree_points:
		if (tp as Vector2).distance_to(p) < radius:
			return true
	return false

## A spot beside the village, a little beyond its houses, flat dry ground clear of trees and water for the whole mat.
func _pick_spot(tile: int, home: Vector2, village_radius: float) -> Variant:
	var h0: float = b.w.heights[tile]
	var seed := M7WorldData.hash01(tile, 0, 81)
	var best: Variant = null
	var best_score := -1000
	for k in 18:
		var a := seed * TAU + k * 2.39996
		var d := village_radius + 0.42 + 0.05 * (k / 6)
		var centre := home + Vector2(cos(a), sin(a)) * d
		var axis := a + PI * 0.5 + (M7WorldData.hash01(tile, k, 82) - 0.5) * 0.6
		var dir := Vector2(cos(axis), -sin(axis))      # local X in world XZ, matching Basis(UP, axis)
		var score := 0
		for off in [-0.2, -0.1, 0.0, 0.1, 0.2]:
			var q: Vector2 = centre + dir * (float(off) * MAT_SCALE)
			if b._ok_site(q, h0):
				score += 1
		if _tree_near(centre, 0.4):
			score -= 4
		if score > best_score:
			best_score = score
			best = [centre, axis]
		if score == 5:
			break
	return best

func sync(tick: float) -> void:
	var w: M7WorldData = b.w
	var sig := hash(w.feast_learned)
	for s in w.settlements:
		sig = sig * 31 + int(s.tile) * 7 + int(s.civ) + (1 if int(s.population) > 0 and not bool(s.get("remembered")) else 0) + int(s.population / 8) * 3
	if sig == signature:
		return
	signature = sig
	for site in sites:
		for k in ["mat", "flame", "light", "seated", "cheer", "host", "shadow"]:
			(site[k] as Node).queue_free()
	sites.clear()
	for si in w.settlements.size():
		var s: Dictionary = w.settlements[si]
		var civ := int(s.civ)
		if bool(s.get("remembered")) or int(s.population) <= 0 or civ < 0 or civ >= w.feast_learned.size() or w.feast_learned[civ] < 0:
			continue
		var tile := int(s.tile)
		var home: Vector2 = w.tile_center(tile)
		if si < b.hearths.size():
			home = Vector2(b.hearths[si].x, b.hearths[si].z)
		var radius := 0.3
		for v in b.villages:
			if Vector2(v.x, v.y).distance_to(home) < 0.6:
				radius = v.z
				break
		var spot: Variant = _pick_spot(tile, home, radius)
		if spot == null:
			continue
		var mat := MeshInstance3D.new()
		mat.mesh = b.kit.get_mesh("feast_mat")
		mat.material_override = b.mats.prop
		mat.visible = false
		holder.add_child(mat)
		var flame := MeshInstance3D.new()
		flame.mesh = b.kit.get_mesh("flame")
		flame.material_override = b.mats.fire
		flame.visible = false
		holder.add_child(flame)
		var light := OmniLight3D.new()
		light.light_color = Color("#ff9440")
		light.omni_range = 0.7
		light.omni_attenuation = 1.5
		light.shadow_enabled = false
		light.visible = false
		holder.add_child(light)
		sites.append({
			"tile": tile, "civ": civ, "home": home, "centre": spot[0], "axis": spot[1], "learned": w.feast_learned[civ],
			"guests": clampi(int(int(s.population) * 0.5), 6, MAX_GUESTS), "offset": SPREAD * M7WorldData.hash01(tile, civ, 83),
			"mat": mat, "flame": flame, "light": light, "shown": false,
			"seated": _multimesh("person_feast_seated", b.mats.figure, MAX_GUESTS),
			"cheer": _multimesh("person_feast_cheer", b.mats.figure, MAX_GUESTS),
			"host": _multimesh("person_feast_host", b.mats.figure, 1),
			"shadow": _multimesh("shadow_disc", b.mats.shadow_disc, MAX_GUESTS + 1),
		})

## 0..1 through this year's feast of a village, or -1 when it is not feasting (or has not learned how yet).
func _progress(site: Dictionary, tick: float) -> float:
	var year := int(tick / YEAR)
	for y in [year, year - 1]:
		if y < 0:
			continue
		var start: float = y * YEAR + START + float(site.offset)
		if start >= float(site.learned) and tick >= start and tick <= start + LENGTH:
			return (tick - start) / LENGTH
	return -1.0

func _hide(site: Dictionary) -> void:
	if not bool(site.shown):
		return
	site.shown = false
	for k in ["mat", "flame", "light", "seated", "cheer", "host", "shadow"]:
		(site[k] as Node3D).visible = false

func _world(site: Dictionary, local: Vector3) -> Vector3:
	var c: Vector2 = site.centre
	var basis := Basis(Vector3.UP, float(site.axis))
	var p: Vector3 = basis * (local * MAT_SCALE)
	return Vector3(c.x + p.x, 0.0, c.y + p.z)

func update(tick: float) -> void:
	var seconds := tick * 0.1
	for site in sites:
		var u := _progress(site, tick)
		if u < 0.0:
			_hide(site)
			continue
		_play(site, u, seconds)

func _play(site: Dictionary, u: float, seconds: float) -> void:
	var w: M7WorldData = b.w
	var axis: float = site.axis
	var home: Vector2 = site.home
	var civ_tint := int(site.civ) % 8
	var n: int = site.guests
	if not bool(site.shown):
		site.shown = true
		for k in ["mat", "flame", "light", "seated", "cheer", "host", "shadow"]:
			(site[k] as Node3D).visible = true
	# the mat, food and fire arrive and are cleared away at the ends
	var gear := _smooth(minf(u / 0.08, (1.0 - u) / 0.08))
	var centre: Vector2 = site.centre
	var gy := maxf(w.height_at(centre), 0.0)
	var mat: MeshInstance3D = site.mat
	mat.transform = Transform3D(Basis(Vector3.UP, axis).scaled(Vector3.ONE * MAT_SCALE * gear), Vector3(centre.x, gy + 0.002, centre.y))
	var fire := _world(site, Vector3(FIRE_X, 0.0, 0.0))
	var flame: MeshInstance3D = site.flame
	var flick := 0.8 + 0.2 * sin(seconds * 9.0)
	flame.position = Vector3(fire.x, gy + 0.012 * MAT_SCALE, fire.z)
	flame.scale = Vector3.ONE * (0.55 * gear * flick)
	var light: OmniLight3D = site.light
	light.position = Vector3(fire.x, gy + 0.1, fire.z)
	light.light_energy = 0.32 * gear * flick
	var seated: MultiMesh = (site.seated as MultiMeshInstance3D).multimesh
	var cheer: MultiMesh = (site.cheer as MultiMeshInstance3D).multimesh
	var host: MultiMesh = (site.host as MultiMeshInstance3D).multimesh
	var shadow: MultiMesh = (site.shadow as MultiMeshInstance3D).multimesh
	seated.visible_instance_count = n
	cheer.visible_instance_count = n
	host.visible_instance_count = 1
	shadow.visible_instance_count = n + 1
	var walk := _smooth(u / COME) if u < COME else (1.0 - _smooth((u - GO) / (1.0 - GO)) if u > GO else 1.0)
	var moving := u < COME or u > GO
	var zero := Basis().scaled(Vector3.ZERO)
	for i in n:
		var side := i % 2
		var local := Vector3(SEAT_X[i / 2], 0.0, SEAT_Z if side == 0 else -SEAT_Z)
		var seat := _world(site, local)
		var start := home + Vector2(cos(i * 1.9), sin(i * 1.9)) * (0.08 + 0.06 * M7WorldData.hash01(site.tile, i, 84))
		var p := Vector2(seat.x, seat.z)
		var pos := start.lerp(p, walk)
		var grow := _smooth(minf(u / 0.03, (1.0 - u) / 0.03))
		var face := axis + (PI if side == 0 else 0.0)
		if moving:
			face = atan2((p - start).x, (p - start).y) if u < COME else atan2((start - p).x, (start - p).y)
		var y := maxf(w.height_at(pos), 0.0)
		var up := not moving and sin(seconds * 2.4 + i * 1.9) > 0.55
		var xf := Transform3D(Basis(Vector3.UP, face).scaled(Vector3.ONE * grow), Vector3(pos.x, y, pos.y))
		var off := Transform3D(zero, Vector3(pos.x, y, pos.y))
		var custom := Color(float(civ_tint) + M7WorldData.hash01(site.tile, i, 85) * 0.98, 0.0, 0.012 if moving else 0.0, 0.0)
		# someone standing taller than the ones beside them reads as cheering; stand in the same spot
		seated.set_instance_transform(i, off if (up or moving) else xf)
		cheer.set_instance_transform(i, xf if (up or moving) else off)
		seated.set_instance_custom_data(i, custom)
		cheer.set_instance_custom_data(i, custom)
		shadow.set_instance_transform(i, Transform3D(Basis().scaled(Vector3(grow, 1.0, grow)), Vector3(pos.x, y + 0.001, pos.y)))
		shadow.set_instance_custom_data(i, Color(0, 0, 0, 0))
	# the host stands at the head, facing down the mat
	var hp := _world(site, Vector3(HOST_X, 0.0, 0.0))
	var hpos := Vector2(hp.x, hp.z)
	var hhome := home + Vector2(0.05, 0.07)
	var hmove := hhome.lerp(hpos, walk)
	var hy := maxf(w.height_at(hmove), 0.0) + 0.002 * sin(seconds * 3.0)
	var hface := axis - PI * 0.5
	if moving:
		hface = atan2((hpos - hhome).x, (hpos - hhome).y) if u < COME else atan2((hhome - hpos).x, (hhome - hpos).y)
	var hgrow := _smooth(minf(u / 0.03, (1.0 - u) / 0.03))
	host.set_instance_transform(0, Transform3D(Basis(Vector3.UP, hface).scaled(Vector3.ONE * hgrow), Vector3(hmove.x, hy, hmove.y)))
	host.set_instance_custom_data(0, Color(float(civ_tint) + 0.5, 0.0, 0.012 if moving else 0.0, 0.0))
	shadow.set_instance_transform(n, Transform3D(Basis().scaled(Vector3(hgrow, 1.0, hgrow)), Vector3(hmove.x, hy + 0.001, hmove.y)))
	shadow.set_instance_custom_data(n, Color(0, 0, 0, 0))
