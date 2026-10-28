## Ziggurats (BLD-08 Stepped Pyramid): the life on and around the ziggurat each city raises.
##
## The mesh itself is a static prop laid out with the village (world_builder.gd, `ziggurat_sites`); this rig only adds what moves.
## A finished one keeps a fire on its summit with a thin thread of smoke, and once every dozen seconds a file of six walkers climbs
## the central stair at dawn: the fire swells while they stand at the top, then they walk back down. One still being raised has a
## gang of carriers looping from the brick stacks up the earth ramp with stone and back down the other lane. The walkers are a
## cosmetic party drawn with the ordinary person meshes, not taken from the city's real figures. Everything is a pure function of
## the render tick, so pause, speed and loading all work and nothing is stored.
class_name M7ZigguratRig
extends RefCounted

const SC := 1.3                        # same as M7WorldBuilder.ZIG_SCALE
const PERIOD := 120.0                  # ticks between processions (12 seconds at 1x)
const CLIMB_END := 0.45                # fractions of the period: climb, hold at the top, descend, rest
const HOLD_END := 0.60
const DESCEND_END := 0.95
const FILE_DELAY := 0.012              # the next walker sets out this much of the period later
const WALKERS := 6
const CARRIERS := 8
const CARRY_LOOP := 80.0               # ticks for a carrier to go round once

# the finished ziggurat's central axis, local coordinates (z toward the hearth): stair foot, up each flight, to the altar
const AXIS := [Vector3(0, 0, 0.36), Vector3(0, 0.012, 0.29), Vector3(0, 0.122, 0.172), Vector3(0, 0.122, 0.15),
	Vector3(0, 0.202, 0.083), Vector3(0, 0.202, 0.078), Vector3(0, 0.256, 0.03)]
const FLAME_AT := Vector3(0, 0.272, 0.009)

var b                                  # the M7WorldBuilder
var parent: Node3D
var smoke_factory: Callable
var holder: Node3D
var sites: Array = []
var signature := -1

func _init(builder, parent_node: Node3D, smoke_maker: Callable) -> void:
	b = builder
	parent = parent_node
	smoke_factory = smoke_maker

static func _smooth(x: float) -> float:
	var t := clampf(x, 0.0, 1.0)
	return t * t * (3.0 - 2.0 * t)

## Rebuilds the sites when a ziggurat appears, moves, is finished or changes hands.
func sync() -> void:
	var sig := 17
	for z in b.ziggurat_sites:
		sig = sig * 31 + int(z.tile)
		sig = sig * 31 + int(z.stage)
		sig = sig * 31 + int(z.civ)
		sig = sig * 31 + int(float((z.p as Vector2).x) * 1000.0) + int(float((z.p as Vector2).y) * 777.0)
	if sig == signature:
		return
	signature = sig
	if holder != null:
		holder.queue_free()
	holder = Node3D.new()
	holder.name = "Ziggurats"
	parent.add_child(holder)
	sites.clear()
	for z in b.ziggurat_sites:
		sites.append(_make_site(z))

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

func _make_site(z: Dictionary) -> Dictionary:
	var site := {"z": z, "stage": int(z.stage), "civ": int(z.civ), "seed": float(z.seed), "tile": int(z.tile)}
	var n := WALKERS if int(z.stage) == 4 else CARRIERS
	site["walkers"] = _multimesh("person_plain", b.mats.figure, n)
	site["leader"] = _multimesh("person_elder", b.mats.figure, 1)
	site["load"] = _multimesh("person_carry_stone", b.mats.figure, CARRIERS)
	site["shadow"] = _multimesh("shadow_disc", b.mats.shadow_disc, n + 1)
	if int(z.stage) == 4:
		var flame := MeshInstance3D.new()
		flame.mesh = b.kit.get_mesh("flame")
		flame.material_override = b.mats.fire
		flame.position = _world(z, FLAME_AT)
		holder.add_child(flame)
		var light := OmniLight3D.new()
		light.light_color = Color("#ff9440")
		light.omni_range = 0.8
		light.omni_attenuation = 1.5
		light.shadow_enabled = false
		light.position = _world(z, FLAME_AT + Vector3(0, 0.04, 0))
		holder.add_child(light)
		var smoke: GPUParticles3D = smoke_factory.call()
		M7SignalRig.thicken_smoke(smoke, 0.12, 22, 7.0)
		smoke.position = _world(z, FLAME_AT + Vector3(0, 0.05, 0))
		holder.add_child(smoke)
		site["flame"] = flame
		site["light"] = light
		site["smoke"] = smoke
	return site

## A point in the ziggurat's own space (z toward the hearth, scaled by SC) as a world position.
func _world(z: Dictionary, local: Vector3) -> Vector3:
	var p: Vector2 = z.p
	var o: Vector3 = M7WorldBuilder.rot_y(float(z.yaw)) * (Vector3(local.x, 0.0, local.z) * SC)
	var y := float(z.y) + local.y * SC
	if local.y <= 0.0:
		y = maxf(y, b.w.height_at(Vector2(p.x + o.x, p.y + o.z)))
	return Vector3(p.x + o.x, y, p.y + o.z)

## The point a fraction `s` (0..1) of the way along a polyline, by length, and the heading of that stretch (x, z).
static func _along(path: Array, s: float) -> Array:
	var total := 0.0
	for i in path.size() - 1:
		total += (path[i + 1] as Vector3).distance_to(path[i])
	var want := clampf(s, 0.0, 1.0) * total
	for i in path.size() - 1:
		var a: Vector3 = path[i]
		var c: Vector3 = path[i + 1]
		var seg_len := a.distance_to(c)
		if want <= seg_len or i == path.size() - 2:
			var f := 0.0 if seg_len <= 0.0 else clampf(want / seg_len, 0.0, 1.0)
			return [a.lerp(c, f), c - a]
		want -= seg_len
	return [path[0], Vector3.FORWARD]

## The construction route, local space: brick stacks, up the ramp (+z lane), across to the work spot, down the other lane.
static func _carry_path(stage: int) -> Array:
	var rh := 0.07 if stage == 1 else 0.122
	var xf := 0.36 if stage == 1 else 0.42
	var xh := 0.2155 if stage == 1 else 0.207
	var work := Vector3(0.19, 0.122, 0.0) if stage == 3 else Vector3(0.1, rh, 0.0)
	return [Vector3(0.40, 0, 0.075), Vector3(xf, 0, 0.018), Vector3(xh, rh, 0.018), work,
		Vector3(xh, rh, -0.018), Vector3(xf, 0, -0.018), Vector3(0.40, 0, -0.075)]

func update(tick: float) -> void:
	var seconds := tick * 0.1
	for site in sites:
		if int(site.stage) == 4:
			_play_finished(site, tick, seconds)
		else:
			_play_building(site, tick)

func _put(mm: MultiMesh, i: int, pos: Vector3, face: float, grow: float, civ: int, variant: float, walking: bool) -> void:
	mm.set_instance_transform(i, Transform3D(Basis(Vector3.UP, face).scaled(Vector3.ONE * grow), pos))
	mm.set_instance_custom_data(i, Color(float(civ % 8) + variant * 0.98, 0.0, 0.012 if walking else 0.0, 0.0))

func _put_shadow(mm: MultiMesh, i: int, pos: Vector3, grow: float) -> void:
	mm.set_instance_transform(i, Transform3D(Basis().scaled(Vector3(grow, 1.0, grow)), pos + Vector3(0, 0.001, 0)))
	mm.set_instance_custom_data(i, Color(0, 0, 0, 0))

func _play_finished(site: Dictionary, tick: float, seconds: float) -> void:
	var z: Dictionary = site.z
	var civ := int(site.civ)
	var seed := float(site.seed)
	var phase := fposmod(tick + seed * PERIOD, PERIOD) / PERIOD
	# the fire swells while the file stands at the top
	var hold := _smooth((phase - CLIMB_END * 0.9) / (CLIMB_END * 0.2)) * (1.0 - _smooth((phase - HOLD_END) / 0.08))
	var flick := 0.85 + 0.15 * sin(seconds * 8.0 + seed * 9.0)
	var flame: MeshInstance3D = site.flame
	var fs := 0.6 * SC * (1.0 + 0.8 * hold) * flick
	flame.scale = Vector3(fs, fs * (0.95 + 0.4 * hold), fs)
	(site.light as OmniLight3D).light_energy = (0.7 + 0.9 * hold) * flick
	(site.smoke as GPUParticles3D).amount_ratio = 0.5 + 0.5 * hold
	var path: Array = []
	for pt in AXIS:
		path.append(pt)
	var walkers: MultiMesh = (site.walkers as MultiMeshInstance3D).multimesh
	var leader: MultiMesh = (site.leader as MultiMeshInstance3D).multimesh
	var shadow: MultiMesh = (site.shadow as MultiMeshInstance3D).multimesh
	(site.load as MultiMeshInstance3D).multimesh.visible_instance_count = 0
	walkers.visible_instance_count = WALKERS - 1
	leader.visible_instance_count = 1
	shadow.visible_instance_count = WALKERS
	var tick_hash := M7WorldData.hash01(site.tile, civ, 61)
	for k in WALKERS:
		var f := phase - k * FILE_DELAY
		var s := 0.0
		var climbing := true
		if f < 0.0 or f > DESCEND_END:
			s = -1.0
		elif f <= CLIMB_END:
			s = f / CLIMB_END
		elif f <= HOLD_END:
			s = 1.0
		else:
			s = 1.0 - (f - HOLD_END) / (DESCEND_END - HOLD_END)
			climbing = false
		var grow := 0.0 if s < 0.0 else _smooth(minf(s * 12.0, 1.0))
		var seg: Array = _along(path, maxf(s, 0.0))
		var pos := _world(z, seg[0])
		var dir: Vector3 = seg[1]
		var heading: Vector3 = M7WorldBuilder.rot_y(float(z.yaw)) * Vector3(dir.x, 0.0, dir.z)
		var face := atan2(heading.x, heading.z)
		if not climbing:
			face += PI
		var walking := s >= 0.0 and f > 0.0 and (f < CLIMB_END or f > HOLD_END)
		if k == 0:
			_put(leader, 0, pos, face, grow, civ, tick_hash, walking)
		else:
			_put(walkers, k - 1, pos, face, grow, civ, fmod(tick_hash + k * 0.17, 1.0), walking)
		_put_shadow(shadow, k, pos, grow)

func _play_building(site: Dictionary, tick: float) -> void:
	var z: Dictionary = site.z
	var civ := int(site.civ)
	var path := _carry_path(int(site.stage))
	var walkers: MultiMesh = (site.walkers as MultiMeshInstance3D).multimesh
	var loads: MultiMesh = (site.load as MultiMeshInstance3D).multimesh
	var shadow: MultiMesh = (site.shadow as MultiMeshInstance3D).multimesh
	(site.leader as MultiMeshInstance3D).multimesh.visible_instance_count = 0
	walkers.visible_instance_count = CARRIERS
	loads.visible_instance_count = CARRIERS
	shadow.visible_instance_count = CARRIERS
	var rot := M7WorldBuilder.rot_y(float(z.yaw))
	for k in CARRIERS:
		var u := fposmod(tick / CARRY_LOOP + float(k) / CARRIERS, 1.0)
		var seg: Array = _along(path, u)
		var pos := _world(z, seg[0])
		var dir: Vector3 = seg[1]
		var heading: Vector3 = rot * Vector3(dir.x, 0.0, dir.z)
		var face := atan2(heading.x, heading.z)
		var loaded := 1.0 if u < 0.5 else 0.0
		var variant := M7WorldData.hash01(site.tile, k, 62)
		_put(walkers, k, pos, face, 1.0, civ, variant, true)
		_put(loads, k, pos, face, loaded, civ, variant, true)
		_put_shadow(shadow, k, pos, 1.0)
