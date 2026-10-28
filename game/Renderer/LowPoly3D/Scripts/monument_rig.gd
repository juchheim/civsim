## The megalith a civilization raises (STN-10): a ring of eight standing stones capped by four lintels.
##
## The simulation has no megalith structure; the frame only says where it stands, whose it is and when the project was learned
## (MatchFrame.Monument*). The raising is played from that tick, one piece a season (the "seasonal festival"): a crew drags the
## stone from outside the ring on a sledge over rollers, hauls it to its socket, tips it upright on its foot (an upright) or
## levers it up onto its neighbours (a lintel), then walks home. Everything is a pure function of the render tick, so pausing,
## speeding up and loading a game all just work. The crew is a cosmetic gang drawn with the ordinary person mesh; it is not
## taken from the village's real figures.
class_name M7MonumentRig
extends RefCounted

const UPRIGHTS := 8
const STEPS := 12                  # eight uprights, then four lintels
const STEP_TICKS := 150.0          # one season per piece
const RING_R := 0.34
const SC := 1.3                    # monument pieces are drawn larger than life, like the houses
const STONE_H := 0.17 * SC
const HAUL_FROM := 1.5             # how far outside the ring the stone starts
const MAX_CREW := 15

# phases of one step, as fractions of its season
const P_GATHER := 0.12             # crew walks out to the stone
const P_HAUL := 0.64               # end of the haul
const P_RAISE := 0.86              # end of the raising
# (the rest: the crew walks home)

var b                              # the M7WorldBuilder: world data, kit, materials, site tests
var holder: Node3D
var sites: Array = []
var signature := -1

func _init(builder) -> void:
	b = builder

## Called with the frame's monument list (stride 5: tile, civ, start tick, population, id) after the village layout is built.
func set_monuments(data: PackedInt32Array, structure_signature: int) -> void:
	var sig := hash(data) * 31 + structure_signature
	if sig == signature:
		return
	signature = sig
	if holder != null:
		holder.queue_free()
	holder = Node3D.new()
	holder.name = "Monuments"
	b.root.add_child(holder)
	sites.clear()
	for i in range(0, data.size(), 5):
		var site := _make_site(data[i], data[i + 1], data[i + 2], data[i + 3])
		if not site.is_empty():
			sites.append(site)

func _land(p: Vector2) -> bool:
	var t: int = b.w.tile_at(p)
	return t >= 0 and b.w.is_land(t)

func _ground(p: Vector2) -> float:
	return maxf(b.w.height_at(p), 0.0)

func _village_radius(c0: Vector2) -> float:
	for v in b.villages:
		if Vector2(v.x, v.y).distance_to(c0) < 0.6:
			return v.z
	return 0.3

## Finds flat dry ground beside the village for the ring, preferring a spot whose socket points are all usable.
func _pick_center(tile: int) -> Variant:
	var c0: Vector2 = b.w.tile_center(tile)
	var h0: float = b.w.heights[tile]
	var seed := M7WorldData.hash01(tile, 0, 21)
	var base_r := _village_radius(c0) + RING_R + 0.42
	var best: Variant = null
	var best_score := -1000
	for k in 36:
		var a := seed * TAU + k * 2.39996
		var cand := c0 + Vector2(cos(a), sin(a)) * (base_r + 0.06 * (k / 6))
		var score := 0
		# trees inside or just outside the ring would stand through the stones
		for tp in b.tree_points:
			if (tp as Vector2).distance_to(cand) < RING_R + 0.3:
				score -= 6
		# a ziggurat (BLD-08) stands about the same distance out: keep the ring off it
		for z in b.ziggurat_sites:
			if (z.p as Vector2).distance_to(cand) < RING_R + 0.6:
				score -= 20
		if b._ok_site(cand, h0):
			score += 4
		for i in UPRIGHTS:
			var ang := TAU * i / UPRIGHTS
			if b._ok_site(cand + Vector2(cos(ang), sin(ang)) * RING_R, h0):
				score += 1
		if score > best_score:
			best_score = score
			best = [cand, a]
		if score == 12:
			break
	return best

func _make_site(tile: int, civ: int, start: int, pop: int) -> Dictionary:
	if tile < 0 or tile >= b.w.width * b.w.height:
		return {}
	var picked: Variant = _pick_center(tile)
	if picked == null:
		return {}
	var c: Vector2 = picked[0]
	var home: Vector2 = b.w.tile_center(tile)
	var site := {
		"tile": tile, "civ": civ, "start": float(start), "center": c, "home": home, "last_step": -2,
		"crew_n": clampi(int(pop * 0.45), 6, MAX_CREW), "seed": M7WorldData.hash01(tile, civ, 22),
		"dirs": [], "slots": [], "scale_y": [], "haul_len": [],
	}
	for i in UPRIGHTS:
		var ang := TAU * i / UPRIGHTS + 0.2 * (M7WorldData.hash01(tile, 0, 23) - 0.5)
		var dir := Vector2(cos(ang), sin(ang))
		site.dirs.append(dir)
		site.slots.append(c + dir * RING_R)
		site.scale_y.append(0.94 + 0.12 * M7WorldData.hash01(tile, i, 24))
		# the stone comes in along its own radius, from as far out as the ground allows
		var len := HAUL_FROM
		while len > 0.5 and not _land(c + dir * (RING_R + len)):
			len -= 0.25
		site.haul_len.append(len)
	var pieces: Array = []
	for k in STEPS:
		var mesh_name := "monument_menhir" if k < UPRIGHTS else "monument_lintel"
		var node := MeshInstance3D.new()
		node.mesh = b.kit.get_mesh(mesh_name)
		node.material_override = b.mats.prop
		node.visible = false
		holder.add_child(node)
		pieces.append(node)
	site["pieces"] = pieces
	var sledge := MeshInstance3D.new()
	sledge.mesh = b.kit.get_mesh("monument_sledge")
	sledge.material_override = b.mats.prop
	sledge.visible = false
	holder.add_child(sledge)
	site["sledge"] = sledge
	var rollers: Array = []
	for i in 5:
		var roller := MeshInstance3D.new()
		roller.mesh = b.kit.get_mesh("monument_roller")
		roller.material_override = b.mats.prop
		roller.visible = false
		holder.add_child(roller)
		rollers.append(roller)
	site["rollers"] = rollers
	var ropes: Array = []
	for i in 3:
		var rope := MeshInstance3D.new()
		rope.mesh = b.kit.get_mesh("monument_rope")
		rope.material_override = b.mats.prop
		rope.visible = false
		holder.add_child(rope)
		ropes.append(rope)
	site["ropes"] = ropes
	# the crew: person figures and their shadows as two multimeshes
	site["crew"] = _crew_multimesh("person_plain", b.mats.figure)
	site["crew_shadow"] = _crew_multimesh("shadow_disc", b.mats.shadow_disc)
	return site

func _crew_multimesh(mesh_name: String, material: Material) -> MultiMeshInstance3D:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_custom_data = true
	mm.mesh = b.kit.get_mesh(mesh_name)
	mm.instance_count = MAX_CREW
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

func _v3(p: Vector2, y: float) -> Vector3:
	return Vector3(p.x, y, p.y)

## A basis whose Y axis is `up`, X horizontal and perpendicular to `along`, scaled.
func _basis(up: Vector3, along: Vector2, sx: float, sy: float, sz: float) -> Basis:
	var side := Vector3.UP.cross(Vector3(along.x, 0.0, along.y)).normalized()
	var y := up.normalized()
	var z := side.cross(y).normalized()
	return Basis(side * sx, y * sy, z * sz)

func _upright_final(site: Dictionary, k: int) -> Transform3D:
	var slot: Vector2 = site.slots[k]
	var yaw := Basis(Vector3.UP, TAU * M7WorldData.hash01(site.tile, k, 25))
	return Transform3D(yaw.scaled(Vector3(SC, SC * float(site.scale_y[k]), SC)), _v3(slot, _ground(slot)))

func _lintel_geometry(site: Dictionary, j: int) -> Array:
	var ang := (2.0 * j + 0.5) * (TAU / UPRIGHTS)
	var dir := Vector2(cos(ang), sin(ang))
	var rc := RING_R * cos(PI / UPRIGHTS)
	return [dir, rc]

func _lintel_final(site: Dictionary, j: int) -> Transform3D:
	var g := _lintel_geometry(site, j)
	var dir: Vector2 = g[0]
	var p: Vector2 = (site.center as Vector2) + dir * float(g[1])
	return Transform3D(_basis(Vector3.UP, dir, SC, SC, SC), _v3(p, _ground(p) + STONE_H + 0.02 * SC))

func _hide_haul_gear(site: Dictionary) -> void:
	(site.sledge as Node3D).visible = false
	for r in site.rollers:
		(r as Node3D).visible = false
	for r in site.ropes:
		(r as Node3D).visible = false
	(site.crew as MultiMeshInstance3D).multimesh.visible_instance_count = 0
	(site.crew_shadow as MultiMeshInstance3D).multimesh.visible_instance_count = 0

## Every frame: pieces before the current step stand in place, the current one is being raised, later ones are not there yet.
func update(render_tick: float) -> void:
	for site in sites:
		var age := render_tick - float(site.start)
		var step := -1
		if age >= STEPS * STEP_TICKS:
			step = STEPS
		elif age >= 0.0:
			step = int(floor(age / STEP_TICKS))
		if step != site.last_step:
			site.last_step = step
			for k in STEPS:
				var node: MeshInstance3D = site.pieces[k]
				node.visible = k < step
				if k < step:
					node.transform = _upright_final(site, k) if k < UPRIGHTS else _lintel_final(site, k - UPRIGHTS)
		if step < 0 or step >= STEPS:
			_hide_haul_gear(site)
			continue
		_play_step(site, step, (age - step * STEP_TICKS) / STEP_TICKS)

## One piece's season: the crew gathers, drags it in, raises it, and goes home.
func _play_step(site: Dictionary, k: int, u: float) -> void:
	var node: MeshInstance3D = site.pieces[k]
	var c: Vector2 = site.center
	var upright := k < UPRIGHTS
	var dir: Vector2
	var socket: Vector2         # where the piece ends up on the ground plane (the foot, or the lintel's lead-in point)
	var reach: float
	if upright:
		dir = site.dirs[k]
		socket = site.slots[k]
		reach = float(site.haul_len[k])
	else:
		var g := _lintel_geometry(site, k - UPRIGHTS)
		dir = g[0]
		socket = c + dir * (float(g[1]) + 0.2)
		reach = 1.2
	var d_in := -dir
	var e := _smooth((u - P_GATHER) / (P_HAUL - P_GATHER))
	var travelled := reach * (1.0 - e)                 # distance still to go
	var base := socket + dir * travelled               # the foot (upright) or the centre (lintel) of the piece on the ground
	var gy := _ground(base)
	var tilt := 0.0                                      # 0 standing / lifted, 1 lying / still low
	var lifted := 0.0
	if u > P_HAUL:
		var r := _smooth((u - P_HAUL) / (P_RAISE - P_HAUL))
		tilt = 1.0 - r
		lifted = r
	else:
		tilt = 1.0
	var hauling := u < P_RAISE
	# --- the piece itself
	node.visible = true
	if upright:
		var theta := tilt * PI * 0.5
		var up := Vector3.UP * cos(theta) + Vector3(dir.x, 0.0, dir.y) * sin(theta)
		var basis := _basis(up, dir, SC, SC * float(site.scale_y[k]), SC)
		node.transform = Transform3D(basis, _v3(base, gy + 0.05 * SC * sin(theta)))
	else:
		var low := _v3(base, gy + 0.03 * SC + 0.02 * SC)
		var top := _lintel_final(site, k - UPRIGHTS).origin
		var pos := low.lerp(top, lifted)
		node.transform = Transform3D(_basis(Vector3.UP, dir, SC, SC, SC), pos)
	# --- sledge, rollers
	var sledge: MeshInstance3D = site.sledge
	var rollers: Array = site.rollers
	var gear := u < P_RAISE + 0.05
	sledge.visible = gear
	var centre := base + dir * (0.085 * SC if upright else 0.0)
	if gear:
		sledge.transform = Transform3D(_basis(Vector3.UP, dir, SC, SC, SC), _v3(centre, _ground(centre)))
	for i in rollers.size():
		var roller: MeshInstance3D = rollers[i]
		var on := gear and u < P_HAUL + 0.04
		roller.visible = on
		if on:
			var offs := fposmod(i * 0.05 + travelled * 0.5, 0.25) - 0.115
			var rp := centre + d_in * offs
			roller.transform = Transform3D(_basis(Vector3.UP, dir, SC, SC, SC), _v3(rp, _ground(rp) + 0.006 * SC))
	# --- crew
	var n: int = site.crew_n
	var front := base + d_in * (0.02 if upright else 0.1)
	var side := Vector2(d_in.y, -d_in.x)
	var home_centre: Vector2 = site.home
	var crew_mm: MultiMesh = (site.crew as MultiMeshInstance3D).multimesh
	var shadow_mm: MultiMesh = (site.crew_shadow as MultiMeshInstance3D).multimesh
	crew_mm.visible_instance_count = n
	shadow_mm.visible_instance_count = n
	var rows := int(ceil(n / 3.0))
	var civ_tint: int = int(site.civ) % 8
	for i in n:
		var col := i % 3
		var row := i / 3
		# each walker heads for its own spot beside the village hearth
		var ha := TAU * M7WorldData.hash01(site.tile, i, 26)
		var home := home_centre + Vector2(cos(ha), sin(ha)) * (0.06 + 0.1 * M7WorldData.hash01(site.tile, i, 27))
		var lateral := (col - 1) * 0.065
		var stand := front + d_in * (0.13 + 0.055 * row) + side * lateral
		var pos2 := stand
		var scale_f := 1.0
		var walking := false
		if u < P_GATHER:
			var f := _smooth(u / P_GATHER)
			# they walk out from the village to the stone
			pos2 = home.lerp(stand, f)
			walking = f < 0.98
			scale_f = _smooth(u / 0.04)
		elif u < P_HAUL:
			walking = true
		elif u > P_RAISE:
			var f2 := _smooth((u - P_RAISE) / (1.0 - P_RAISE))
			pos2 = stand.lerp(home, f2)
			walking = f2 < 0.98
			scale_f = 1.0 - _smooth((u - 0.96) / 0.04)
		var face := atan2(d_in.x, d_in.y)
		if u < P_GATHER or u > P_RAISE:
			var to := (stand - home) if u < P_GATHER else (home - stand)
			if to.length() > 0.001:
				face = atan2(to.x, to.y)
		var y := _ground(pos2)
		var xf := Transform3D(Basis(Vector3.UP, face).scaled(Vector3(scale_f, scale_f, scale_f)), _v3(pos2, y))
		crew_mm.set_instance_transform(i, xf)
		var hop := Color(float(civ_tint) + float(site.seed) * 0.98 + 0.0, 0.0, 0.012 if walking else 0.0, 0.0)
		crew_mm.set_instance_custom_data(i, hop)
		shadow_mm.set_instance_transform(i, Transform3D(Basis().scaled(Vector3(scale_f, 1.0, scale_f)), _v3(pos2, y + 0.001)))
		shadow_mm.set_instance_custom_data(i, Color(0.0, 0.0, 0.0, 0.0))
	# --- ropes from the piece's leading end to each column of pullers
	var ropes: Array = site.ropes
	var rope_len := 0.13 + 0.055 * (rows - 1)
	for i in ropes.size():
		var rope: MeshInstance3D = ropes[i]
		var on_rope := hauling and u > P_GATHER * 0.8 and u < P_RAISE
		rope.visible = on_rope
		if on_rope:
			var rp2 := front + side * ((i - 1) * 0.065)
			var rb := Basis(Vector3(side.x, 0.0, side.y), Vector3.UP, Vector3(d_in.x, 0.0, d_in.y) * rope_len)
			rope.transform = Transform3D(rb, _v3(rp2, _ground(rp2) + 0.035))
