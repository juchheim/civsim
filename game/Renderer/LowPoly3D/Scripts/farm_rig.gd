## The ard at work (AGR-05 Ard Plough) and the fields at rest (AGR-07 Fallow & Manuring), and the ox carts of the trails (TRN-05).
##
## The simulation has no oxen or carts as agents; the frame only says what each civilization knows (MatchFrame.CivLooks). So these
## are a cosmetic crowd played from the render tick, like the dogs: about one ard team to every four ploughed fields, each walking
## the furrows of its field up and down, turning on the headland and stepping to the next row; two cattle grazing every resting
## field; and an ox cart shuttling along each trail between villages. Every position is a pure function of the tick, so pause,
## speed and loading all work and nothing is stored. The legs swap by distance walked, so the gait keeps its pace at any speed.
class_name M7FarmRig
extends RefCounted

const SIZE := 1.2                   # the figure material already scales everything up with the camera distance
const PLOUGH_SPEED := 0.05          # world units per second along the longest furrow (shorter rows are walked a little slower)
const HEADLAND := 0.1               # the team turns this far inside the field's edge
const TURN_TIME := 5.0              # seconds to swing round on the headland and step to the next row
const ROWS := 7                     # furrows an ard team works in a field before it goes back the other way
const ROW_REACH := 0.3             # the rows lie within this distance of the field's middle line
const STRIDE := 0.04                # distance walked per leg swap
const TEAM_SHARE := 0.25            # one team to every four ploughed fields
const MAX_TEAMS := 18
const MAX_GRAZERS := 36
const CART_SPEED := 0.1
const CART_DWELL := 7.0
const CART_TRIM := 0.45
const CART_MIN := 0.6
const CART_BEHIND := 0.2
const MAX_CARTS := 28
const CATTLE := [[1, 2], [3, 5], [1, 6], [3, 1]]     # [species, shade bin] into the animal tints: aurochs-dark, bison-brown ...

var b                               # the M7WorldBuilder
var holder: Node3D
var teams: Array = []               # {civ, tile, centre, angle, id, phase}
var grazers: Array = []             # {civ, tile, centre, id, spot}
var carts: Array = []               # {civ, tile, id, pts, cum, length, phase}
var slots := {}                     # mesh name -> MultiMeshInstance3D
var used := {}
var shadow: MultiMeshInstance3D
var shadows_used := 0
var signature := -1
var last_tick := -1.0

func _init(builder, parent: Node3D) -> void:
	b = builder
	holder = Node3D.new()
	holder.name = "Farm"
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
	holder.add_child(inst)
	return inst

static func _smooth(x: float) -> float:
	var t := clampf(x, 0.0, 1.0)
	return t * t * (3.0 - 2.0 * t)

## Rebuilds the crowd when who knows what, the fields, the villages or the trails change.
func sync() -> void:
	var w: M7WorldData = b.w
	var sig := hash(w.civ_looks) * 31 + hash(w.trail_meta) * 17 + hash(w.trail_tiles) + w.year / 2 * 7
	for i in range(0, w.plots.size(), 2):
		sig = sig * 31 + w.plots[i] * 3 + w.plots[i + 1] + int(w.owner[w.plots[i]])
	if sig == signature:
		return
	signature = sig
	for inst in slots.values():
		(inst as Node).queue_free()
	slots.clear()
	used.clear()
	if shadow != null:
		shadow.queue_free()
		shadow = null
	teams.clear()
	grazers.clear()
	carts.clear()
	_find_fields()
	_find_carts()
	var total := teams.size() + carts.size() + grazers.size()
	last_tick = -1.0
	if total == 0:
		return
	if not teams.is_empty():
		for pose in ["a", "b"]:
			slots["ard_team_" + pose] = _multimesh("ard_team_" + pose, b.mats.figure, teams.size())
	if not carts.is_empty():
		for pose in ["a", "b"]:
			slots["ox_cart_" + pose] = _multimesh("ox_cart_" + pose, b.mats.figure, carts.size())
	if not grazers.is_empty():
		slots["animal_heavy"] = _multimesh("animal_heavy", b.mats.animal, grazers.size() * 2)
	shadow = _multimesh("shadow_disc", b.mats.shadow_disc, teams.size() + carts.size() + grazers.size() * 2)

func _find_fields() -> void:
	var w: M7WorldData = b.w
	var team_civs := {}
	for i in range(0, w.plots.size(), 2):
		if w.plots[i + 1] != 0:
			continue
		var tile := w.plots[i]
		if not w.is_land(tile):
			continue
		var civ: int = w.owner[tile]
		if civ < 0:
			continue
		var kind: int = b.field_kind(tile)
		var centre: Vector2 = w.tile_center(tile)
		if kind == 1 and teams.size() < MAX_TEAMS and (not team_civs.has(civ) or M7WorldData.hash01(tile, 0, 420) < TEAM_SHARE):
			team_civs[civ] = true
			teams.append({
				"civ": civ, "tile": tile, "centre": centre, "angle": b.field_angle(tile), "id": teams.size(),
				"phase": M7WorldData.hash01(tile, 1, 421),
			})
		elif kind == 2 and grazers.size() < MAX_GRAZERS:
			grazers.append({"civ": civ, "tile": tile, "centre": centre, "id": grazers.size(), "spot": M7WorldData.hash01(tile, 2, 422)})

func _find_carts() -> void:
	var w: M7WorldData = b.w
	for ti in range(0, w.trail_meta.size(), 5):
		var civ := w.trail_meta[ti + 1]
		if civ < 0 or not w.has_look(civ, M7WorldData.LOOK_CARTS):
			continue
		var start := w.trail_meta[ti + 3]
		var length := w.trail_meta[ti + 4]
		var run := PackedInt32Array()
		for j in length:
			var tile := w.trail_tiles[start + j]
			if tile >= 0:
				run.append(tile)
			if (tile < 0 or j == length - 1) and run.size() > 0:
				_add_cart(run, civ, ti)
				run = PackedInt32Array()
				if carts.size() >= MAX_CARTS:
					return

func _add_cart(run: PackedInt32Array, civ: int, trail: int) -> void:
	if run.size() < 2:
		return
	var pts := M7TrailBuilder._smooth(b.w, run)
	var cum := PackedFloat32Array()
	var total := 0.0
	cum.append(0.0)
	for i in range(1, pts.size()):
		total += pts[i].distance_to(pts[i - 1])
		cum.append(total)
	if total - 2.0 * CART_TRIM < CART_MIN:
		return
	for k in (2 if total > 3.0 else 1):
		if carts.size() >= MAX_CARTS:
			return
		carts.append({
			"civ": civ, "tile": run[0], "id": carts.size(), "pts": pts, "cum": cum, "length": total,
			"phase": fmod(M7WorldData.hash01(run[0], trail, 423) + 0.5 * k, 1.0),
		})

func _put(mesh_name: String, xf: Transform3D, palette: float, seed: float) -> void:
	var mm: MultiMesh = (slots[mesh_name] as MultiMeshInstance3D).multimesh
	var i: int = used.get(mesh_name, 0)
	if i >= mm.instance_count:
		return
	mm.set_instance_transform(i, xf)
	mm.set_instance_custom_data(i, Color(palette + seed * 0.98, 0.0, 0.0, 0.0))
	used[mesh_name] = i + 1

func _put_shadow(p: Vector2, y: float, dir: Vector2, length: float, width: float) -> void:
	var mm: MultiMesh = shadow.multimesh
	if shadows_used >= mm.instance_count:
		return
	var basis := Basis(Vector3.UP, M7DogRig._face(dir)).scaled(Vector3(length, 1.0, width))
	mm.set_instance_transform(shadows_used, Transform3D(basis, Vector3(p.x, y + 0.001, p.y)))
	mm.set_instance_custom_data(shadows_used, Color(0, 0, 0, 0))
	shadows_used += 1

func update(tick: float) -> void:
	if slots.is_empty() or tick == last_tick:
		return
	last_tick = tick
	for k in slots:
		used[k] = 0
	shadows_used = 0
	var seconds := tick * 0.1
	for team in teams:
		_play_team(team, seconds)
	for grazer in grazers:
		_play_grazer(grazer, seconds)
	for cart in carts:
		_play_cart(cart, seconds)
	for k in slots:
		(slots[k] as MultiMeshInstance3D).multimesh.visible_instance_count = used.get(k, 0)
	if shadow != null:
		shadow.multimesh.visible_instance_count = shadows_used

## The row the team is on, the one it goes to next and how far through the pass it is, from time. Rows are worked 0..N-1 and back;
## every pass takes the same time (the longest row at PLOUGH_SPEED), so a short row is walked a little slower.
func _plough_state(team: Dictionary, seconds: float) -> Dictionary:
	var pass_time := _pass_time()
	var tt := seconds + float(team.phase) * 400.0
	var plan := ROWS - 1
	var cycle := 2 * plan
	var index := int(floor(tt / pass_time))
	var step := index % cycle
	var row := step if step <= plan else cycle - step
	var next_step := (index + 1) % cycle
	var next_row := next_step if next_step <= plan else cycle - next_step
	return {"row": row, "next": next_row, "local": tt - index * pass_time, "index": index}

static func _pass_time() -> float:
	var run := 2.0 * (M7MeshKit._hex_half_width(0.0, M7WorldData.R * 0.84) - HEADLAND)
	return run / PLOUGH_SPEED + TURN_TIME

func _row_z(row: int) -> float:
	return -ROW_REACH + 2.0 * ROW_REACH * float(row) / float(ROWS - 1)

func _play_team(team: Dictionary, seconds: float) -> void:
	var w: M7WorldData = b.w
	var fr := M7WorldData.R * 0.84
	var st := _plough_state(team, seconds)
	var row: int = st.row
	var z0 := _row_z(row)
	var z1 := _row_z(int(st.next))
	var half := maxf(minf(M7MeshKit._hex_half_width(z0, fr), M7MeshKit._hex_half_width(z1, fr)) - HEADLAND, 0.08)
	var run := 2.0 * half
	var go_right: bool = int(st.index) % 2 == 0
	var local: float = st.local
	var travel := _pass_time() - TURN_TIME
	var lx := 0.0
	var lz := z0
	var heading := 0.0                  # angle of the team's direction in the field's own frame (0 = +X)
	var walked := 0.0
	if local < travel:
		var u := local / travel
		lx = (-half + run * u) if go_right else (half - run * u)
		heading = 0.0 if go_right else PI
		walked = run * u
	else:
		# the turn: out past the row end, round, and onto the next row
		var u := (local - travel) / TURN_TIME
		var end_x := half if go_right else -half
		var dirx := 1.0 if go_right else -1.0
		var swing := _smooth(u)
		lx = end_x + dirx * 0.07 * sin(PI * u)
		lz = lerpf(z0, z1, swing)
		heading = (0.0 if go_right else PI) + (PI if (go_right) else -PI) * swing * (1.0 if z1 >= z0 else -1.0)
		walked = run + u * 0.09
	var a: float = team.angle
	# the field's frame to the world: rot_y(a) turns local +X into (cos a, -sin a)
	var rx := lx * cos(a) + lz * sin(a)
	var rz := -lx * sin(a) + lz * cos(a)
	var pos: Vector2 = Vector2(team.centre) + Vector2(rx, rz)
	var dir_local := Vector2(cos(heading), sin(heading))     # in field frame: x along rows, y across
	var dir := Vector2(dir_local.x * cos(a) + dir_local.y * sin(a), -dir_local.x * sin(a) + dir_local.y * cos(a))
	var y := maxf(w.height_at(pos), 0.0)
	var pose := "a" if int(floor(walked / STRIDE)) % 2 == 0 else "b"
	team["now"] = pos
	var xf := Transform3D(Basis(Vector3.UP, M7DogRig._face(dir)).scaled(Vector3.ONE * SIZE), Vector3(pos.x, y, pos.y))
	_put("ard_team_" + pose, xf, float(int(team.civ) % 8), M7WorldData.hash01(int(team.tile), int(team.id), 424))
	_put_shadow(pos + dir * 0.08 * SIZE, y, dir, 3.4 * SIZE, 1.5)

func _play_grazer(grazer: Dictionary, seconds: float) -> void:
	var w: M7WorldData = b.w
	var centre: Vector2 = grazer.centre
	for k in 2:
		var seed := M7WorldData.hash01(int(grazer.tile), k, 425)
		var t := seconds * 0.05 + float(grazer.spot) * 40.0 + k * 17.0
		# each cow drifts slowly between a few grazing spots on the field and stands head-down for a while
		var a := TAU * (0.5 * k + 0.37 * float(grazer.spot)) + 1.3 * sin(t * 0.7) + 0.6 * sin(t * 1.9 + seed * 9.0)
		var r := 0.12 + 0.1 * (0.5 + 0.5 * sin(t * 1.1 + seed * 5.0))
		var pos := centre + Vector2(cos(a), sin(a)) * r
		var slope := Vector2(cos(a + PI * 0.5), sin(a + PI * 0.5)) * 0.2 * cos(t * 0.7)
		var dir := (slope + Vector2(cos(a * 2.0 + k), sin(a * 2.0 + k)) * 0.3).normalized()
		var tile: int = w.tile_at(pos)
		if tile < 0 or not w.is_land(tile):
			continue
		var y: float = w.heights[tile]
		var cow: Array = CATTLE[(int(grazer.id) * 2 + k) % CATTLE.size()]
		var xf := Transform3D(Basis(Vector3.UP, M7DogRig._face(dir)).scaled(Vector3.ONE * 0.9), Vector3(pos.x, y, pos.y))
		_put("animal_heavy", xf, float(int(cow[0]) * 8 + int(cow[1])), seed)

func _play_cart(cart: Dictionary, seconds: float) -> void:
	var w: M7WorldData = b.w
	var length: float = cart.length
	var run := length - 2.0 * CART_TRIM
	var leg := run / CART_SPEED
	var period := 2.0 * (leg + CART_DWELL)
	var tt := fposmod(seconds + float(cart.phase) * period, period)
	var s := CART_TRIM
	var forward := true
	var walking := true
	var turning := 0.0
	if tt < leg:
		s = CART_TRIM + run * tt / leg
	elif tt < leg + CART_DWELL:
		s = length - CART_TRIM
		walking = false
		turning = _smooth((tt - leg) / CART_DWELL * 1.5 - 0.4)
	elif tt < 2.0 * leg + CART_DWELL:
		s = length - CART_TRIM - run * (tt - leg - CART_DWELL) / leg
		forward = false
	else:
		walking = false
		forward = false
		turning = _smooth((tt - 2.0 * leg - CART_DWELL) / CART_DWELL * 1.5 - 0.4)
	var here := M7DogRig._along(cart, s)
	var pos: Vector2 = here[0]
	var heading: Vector2 = here[1] if forward else -(here[1] as Vector2)
	if not walking:
		# at the end of the track the team swings round before it sets off again
		var angle := lerp_angle(M7DogRig._face(heading), M7DogRig._face(-heading), turning)
		heading = Vector2(cos(angle), -sin(angle))
	else:
		# the cart trails the oxen along the track: aim the whole outfit from a point behind to the oxen's place
		var behind: Vector2 = M7DogRig._along(cart, s - CART_BEHIND if forward else s + CART_BEHIND)[0]
		if behind.distance_to(pos) > 0.01:
			heading = (pos - behind).normalized()
	var y := maxf(w.height_at(pos), 0.0)
	var pose := "a" if (not walking or int(floor(s / STRIDE)) % 2 == 0) else "b"
	cart["now"] = pos
	var xf := Transform3D(Basis(Vector3.UP, M7DogRig._face(heading)).scaled(Vector3.ONE * SIZE), Vector3(pos.x, y, pos.y))
	_put("ox_cart_" + pose, xf, float(int(cart.civ) % 8), M7WorldData.hash01(int(cart.tile), int(cart.id), 426))
	_put_shadow(pos + heading * 0.05 * SIZE, y, heading, 3.6 * SIZE, 1.5)

## Dev aids for scenarios: where the first ard team, resting-field cow and cart are now (the origin when none).
func team_focus() -> Vector3:
	if teams.is_empty() or not teams[0].has("now"):
		return Vector3.ZERO
	var p: Vector2 = teams[0].now
	return Vector3(p.x, maxf(b.w.height_at(p), 0.0), p.y)

func graze_focus() -> Vector3:
	if grazers.is_empty():
		return Vector3.ZERO
	var p: Vector2 = grazers[0].centre
	return Vector3(p.x, maxf(b.w.height_at(p), 0.0), p.y)

func cart_focus() -> Vector3:
	if carts.is_empty() or not carts[0].has("now"):
		return Vector3.ZERO
	var p: Vector2 = carts[0].now
	return Vector3(p.x, maxf(b.w.height_at(p), 0.0), p.y)
