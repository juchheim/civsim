## Dogs (HUS-01 Wolf Companions, TRN-02 Travois & Sledges): once a civilization has tamed the wolf, dogs live in its villages, and
## once it has the travois a dog drags a loaded sledge along each trail between villages.
##
## The simulation has no dog agents (the Dog good is a stock, not a creature); the frame only says how far each civilization has
## come (MatchFrame.CivDogs: 0 none, 1 dogs, 2 travois). So the animals are a cosmetic crowd played from the render tick: about
## one dog to four people in every village, each trotting between a few spots around the houses and then standing, sitting or
## lying down for a while; and one hauling dog per trail run, walking the trail to and fro under its load, pausing at either end.
## The legs swap by the distance walked, so the gait keeps its pace at any game speed. A collar in the civilization's colour says
## whose they are. Everything is a pure function of the tick, so pause, speed and loading all work and nothing is stored.
class_name M7DogRig
extends RefCounted

const POSES := ["trot_a", "trot_b", "stand", "sit", "lie"]
const COATS := 3
const SIZE := 1.2                   # the figure material already scales everything up with the camera distance
const MAX_PER_VILLAGE := 6
const WAYPOINTS := 10               # candidate resting spots around a village, per dog
const SPEED := 0.2                  # world units per second at a trot about the village
const STRIDE := 0.05                # distance walked per leg swap
const SEG_MIN := 7.0                # seconds one trot-and-rest takes (per dog: SEG_MIN .. SEG_MIN + SEG_SPAN)
const SEG_SPAN := 8.0
const MOVE_SHARE := 0.7             # of a segment, at most, is spent on the move
const HAUL_SPEED := 0.12            # a loaded dog walks
const HAUL_DWELL := 6.0             # seconds spent at either end of the trail
const HAUL_TRIM := 0.5              # stay this far from the villages at either end
const HAUL_MIN := 0.6               # a trail shorter than this (after trimming) has no room for a haul
const HAUL_BEHIND := 0.15           # how far back along the track the load's trailing end lies
const MAX_HAULERS := 24

var b                               # the M7WorldBuilder
var holder: Node3D
var dogs: Array = []                # {civ, tile, id, coat, rate, phase, wps}
var haulers: Array = []             # {civ, tile, id, coat, phase, pts, cum, length}
var slots := {}                     # mesh name -> MultiMeshInstance3D
var used := {}                      # mesh name -> instances written this update
var shadow: MultiMeshInstance3D
var shadows_used := 0
var signature := -1
var last_tick := -1.0

func _init(builder, parent: Node3D) -> void:
	b = builder
	holder = Node3D.new()
	holder.name = "Dogs"
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

## Rebuilds the pack when who has dogs, the villages or the trails change.
func sync() -> void:
	var w: M7WorldData = b.w
	var sig := hash(w.dog_civs) * 31 + hash(w.trail_meta) * 17 + hash(w.trail_tiles)
	for s in w.settlements:
		sig = sig * 31 + int(s.tile) * 7 + int(s.civ) + (1 if int(s.population) > 0 and not bool(s.get("remembered")) else 0) + int(s.population / 4) * 3
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
	dogs.clear()
	haulers.clear()
	_find_village_dogs()
	_find_haulers()
	var total := dogs.size() + haulers.size()
	last_tick = -1.0
	if total == 0:
		return
	for coat in COATS:
		for pose in POSES:
			var mesh_name := "dog_%d_%s" % [coat, pose]
			slots[mesh_name] = _multimesh(mesh_name, b.mats.figure, total)
	if not haulers.is_empty():
		slots["dog_travois"] = _multimesh("dog_travois", b.mats.figure, haulers.size())
	shadow = _multimesh("shadow_disc", b.mats.shadow_disc, total)

func _find_village_dogs() -> void:
	var w: M7WorldData = b.w
	for si in w.settlements.size():
		var s: Dictionary = w.settlements[si]
		var civ := int(s.civ)
		var people := int(s.population)
		if bool(s.get("remembered")) or people <= 0 or civ < 0 or civ >= w.dog_civs.size() or w.dog_civs[civ] < 1:
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
		var h0: float = w.heights[tile]
		# the Dog good is one per four people
		var count := clampi(int(round(people / 4.0)), 1, MAX_PER_VILLAGE)
		for d in count:
			var wps := PackedVector2Array()
			for k in WAYPOINTS:
				var a := M7WorldData.hash01(tile, d * 31 + k, 92) * TAU
				var r := radius * 0.4 + (radius + 0.14 - radius * 0.4) * M7WorldData.hash01(tile, d * 31 + k, 93)
				var q := home + Vector2(cos(a), sin(a)) * r
				if b._ok_site(q, h0):
					wps.append(q)
			if wps.is_empty():
				wps.append(home + Vector2(0.06, 0.05) * (1 + d))
			dogs.append({
				"civ": civ, "tile": tile, "id": d, "wps": wps,
				"coat": int(M7WorldData.hash01(tile, d, 94) * COATS) % COATS,
				"rate": M7WorldData.hash01(tile, d, 95), "phase": 100.0 * M7WorldData.hash01(tile, d, 96),
			})

func _find_haulers() -> void:
	var w: M7WorldData = b.w
	for ti in range(0, w.trail_meta.size(), 5):
		var civ := w.trail_meta[ti + 1]
		if civ < 0 or civ >= w.dog_civs.size() or w.dog_civs[civ] < 2:
			continue
		var start := w.trail_meta[ti + 3]
		var length := w.trail_meta[ti + 4]
		var run := PackedInt32Array()
		for j in length:
			var tile := w.trail_tiles[start + j]
			if tile >= 0:
				run.append(tile)
			if (tile < 0 or j == length - 1) and run.size() > 0:
				_add_hauler(run, civ, ti)
				run = PackedInt32Array()
				if haulers.size() >= MAX_HAULERS:
					return

func _add_hauler(run: PackedInt32Array, civ: int, trail: int) -> void:
	if run.size() < 2:
		return
	var pts := M7TrailBuilder._smooth(b.w, run)
	var cum := PackedFloat32Array()
	var total := 0.0
	cum.append(0.0)
	for i in range(1, pts.size()):
		total += pts[i].distance_to(pts[i - 1])
		cum.append(total)
	if total - 2.0 * HAUL_TRIM < HAUL_MIN:
		return
	var id := haulers.size()
	haulers.append({
		"civ": civ, "tile": run[0], "id": id, "pts": pts, "cum": cum, "length": total,
		"coat": int(M7WorldData.hash01(run[0], trail, 97) * COATS) % COATS,
		"phase": M7WorldData.hash01(run[0], trail, 98),
	})

## Where a distance along a hauler's track lies, and which way the track runs there.
static func _along(hauler: Dictionary, s: float) -> Array:
	var pts: PackedVector2Array = hauler.pts
	var cum: PackedFloat32Array = hauler.cum
	var d := clampf(s, 0.0, float(hauler.length))
	var i := 1
	while i < pts.size() - 1 and cum[i] < d:
		i += 1
	var span := maxf(cum[i] - cum[i - 1], 0.0001)
	var p := pts[i - 1].lerp(pts[i], clampf((d - cum[i - 1]) / span, 0.0, 1.0))
	return [p, (pts[i] - pts[i - 1]).normalized()]

## The Y rotation that turns a mesh facing +X to face a ground direction.
static func _face(dir: Vector2) -> float:
	return atan2(-dir.y, dir.x)

func _put(mesh_name: String, xf: Transform3D, civ: int, seed: float) -> void:
	var mm: MultiMesh = (slots[mesh_name] as MultiMeshInstance3D).multimesh
	var i: int = used.get(mesh_name, 0)
	if i >= mm.instance_count:
		return
	mm.set_instance_transform(i, xf)
	mm.set_instance_custom_data(i, Color(float(civ % 8) + seed * 0.98, 0.0, 0.0, 0.0))
	used[mesh_name] = i + 1

func _put_shadow(p: Vector2, y: float, dir: Vector2, length: float) -> void:
	var mm: MultiMesh = shadow.multimesh
	if shadows_used >= mm.instance_count:
		return
	var basis := Basis(Vector3.UP, _face(dir)).scaled(Vector3(length, 1.0, 0.8))
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
	for dog in dogs:
		_play_village_dog(dog, seconds)
	for hauler in haulers:
		_play_hauler(hauler, seconds)
	for k in slots:
		(slots[k] as MultiMeshInstance3D).multimesh.visible_instance_count = used.get(k, 0)
	shadow.multimesh.visible_instance_count = shadows_used

func _pick(dog: Dictionary, segment: int, count: int) -> int:
	return int(M7WorldData.hash01(int(dog.tile), int(dog.id) * 977 + segment + 100000, 99) * count) % count

func _play_village_dog(dog: Dictionary, seconds: float) -> void:
	var w: M7WorldData = b.w
	var wps: PackedVector2Array = dog.wps
	var seg_len := SEG_MIN + SEG_SPAN * float(dog.rate)
	var tt := seconds + float(dog.phase)
	var segment := int(floor(tt / seg_len))
	var local := tt - segment * seg_len
	var origin := wps[_pick(dog, segment - 1, wps.size())]
	var target := wps[_pick(dog, segment, wps.size())]
	var way := target - origin
	var dist := way.length()
	var travel := minf(dist / SPEED, seg_len * MOVE_SHARE)
	var pos := target
	var dir := way.normalized() if dist > 0.001 else Vector2.RIGHT
	var pose := 0
	if dist > 0.02 and local < travel:
		var u := local / travel
		var eased := 0.6 * u + 0.4 * _smooth(u)
		pos = origin.lerp(target, eased)
		pose = int(floor(eased * dist / STRIDE)) % 2
	else:
		# rests where it stopped, a little turned from the way it came
		var r := M7WorldData.hash01(int(dog.tile), int(dog.id) * 977 + segment + 100000, 100)
		pose = 2 if r < 0.3 else (3 if r < 0.7 else 4)
		var turn := (M7WorldData.hash01(int(dog.tile), int(dog.id), segment + 5000) - 0.5) * 1.8
		dir = dir.rotated(turn)
	var y := maxf(w.height_at(pos), 0.0)
	dog["now"] = pos
	var xf := Transform3D(Basis(Vector3.UP, _face(dir)).scaled(Vector3.ONE * SIZE), Vector3(pos.x, y, pos.y))
	_put("dog_%d_%s" % [dog.coat, POSES[pose]], xf, int(dog.civ), M7WorldData.hash01(int(dog.tile), int(dog.id), 101))
	_put_shadow(pos, y, dir, (1.5 if pose < 3 else 1.1) * SIZE)

func _play_hauler(hauler: Dictionary, seconds: float) -> void:
	var w: M7WorldData = b.w
	var length: float = hauler.length
	var run := length - 2.0 * HAUL_TRIM
	var leg := run / HAUL_SPEED
	var period := 2.0 * (leg + HAUL_DWELL)
	var tt := fposmod(seconds + float(hauler.phase) * period, period)
	var s := HAUL_TRIM
	var forward := true
	var walking := true
	var turning := 0.0                          # 0..1 through the pause, when the dog swings round to go back
	if tt < leg:
		s = HAUL_TRIM + run * tt / leg
	elif tt < leg + HAUL_DWELL:
		s = length - HAUL_TRIM
		walking = false
		turning = _smooth((tt - leg) / HAUL_DWELL * 1.6 - 0.5)
	elif tt < 2.0 * leg + HAUL_DWELL:
		s = length - HAUL_TRIM - run * (tt - leg - HAUL_DWELL) / leg
		forward = false
	else:
		walking = false
		forward = false
		turning = _smooth((tt - 2.0 * leg - HAUL_DWELL) / HAUL_DWELL * 1.6 - 0.5)
	var here := _along(hauler, s)
	var pos: Vector2 = here[0]
	var heading: Vector2 = here[1] if forward else -(here[1] as Vector2)
	if not walking:
		# at the end of the track the dog is facing the way it came; it swings round before setting off again
		var back := -heading
		var angle := lerp_angle(_face(heading), _face(back), turning)
		heading = Vector2(cos(angle), -sin(angle))
	var pose := 2
	if walking:
		pose = int(floor(s / STRIDE)) % 2
	var y := maxf(w.height_at(pos), 0.0)
	var coat: int = hauler.coat
	hauler["now"] = pos
	var seed := M7WorldData.hash01(int(hauler.tile), int(hauler.id), 102)
	_put("dog_%d_%s" % [coat, POSES[pose]], Transform3D(Basis(Vector3.UP, _face(heading)).scaled(Vector3.ONE * SIZE), Vector3(pos.x, y, pos.y)), int(hauler.civ), seed)
	# the load trails behind along the track while it moves, and swings with the dog when it turns
	var drag := heading
	if walking:
		var behind: Vector2 = _along(hauler, s - HAUL_BEHIND if forward else s + HAUL_BEHIND)[0]
		if behind.distance_to(pos) > 0.01:
			drag = (pos - behind).normalized()
	_put("dog_travois", Transform3D(Basis(Vector3.UP, _face(drag)).scaled(Vector3.ONE * SIZE), Vector3(pos.x, y, pos.y)), int(hauler.civ), seed)
	_put_shadow(pos, y, heading, 1.5 * SIZE)

## Dev aid for scenarios: where the first village dog is now (the origin when none).
func focus() -> Vector3:
	if dogs.is_empty() or not dogs[0].has("now"):
		return Vector3.ZERO
	var p: Vector2 = dogs[0].now
	return Vector3(p.x, maxf(b.w.height_at(p), 0.0), p.y)

## Dev aid for scenarios: where the first hauling dog is now (the origin when none).
func haul_focus() -> Vector3:
	if haulers.is_empty() or not haulers[0].has("now"):
		return Vector3.ZERO
	var p: Vector2 = haulers[0].now
	return Vector3(p.x, maxf(b.w.height_at(p), 0.0), p.y)
