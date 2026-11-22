## Discovery (E6) ocean sea lanes (docs/MVP/39 §7.2): dashed arcs across open water between harbors that really have a sailed ocean route, with ship tokens
## moving along them, and a floating wreck where a voyage was lost.
##
## Everything here is driven by the lists the Discovery frame will carry (docs/MVP/39 §9: `oceanLanes`, `voyages` and `wrecks`; the names and shapes are
## proposals) and handed to `sync`. With no data nothing is built and nothing is drawn, so Bronze, Iron and Fief are untouched. `load_demo` (the gallery) builds a
## few lanes on the stage so the look can be judged before the sim has the data.
##
##   lane      a dashed ribbon on the water along a gentle arc between two harbors, in a pale tint of the owner's colour; wider with the route's volume (a thin
##             line for a trickle, a bold one for a busy lane), fading out over two idle years (`age` 0 fresh, 1 a year idle, 2 gone) and trimmed short of the harbors
##   token     a caravel or a galleon sailing the lane toward one end or the other (a pure function of the clock), drawn at the waterline over the line; a galleon
##             escorting a convoy sails with the caravels
##   wreck     a floating broken mast, a torn sail, planks and a cask where a voyage was lost (the frame lists the recent ones)
##
## Honest rule (docs/MVP/39 §7.2, A-11): the frame lists only lanes the viewer has sailed or seen; this overlay draws exactly what it is given and never invents a
## lane, so it cannot reveal land. Lines widen with the camera's distance (like the trails) so they are still there at region zoom, and are on by default
## from Z2 outward (docs/MVP/08 §3.3), a decision for the HUD, not the overlay.
class_name M7LaneOverlay
extends RefCounted

const ROUTE_SHADER := "res://Renderer/LowPoly3D/Shaders/route_line.gdshader"
const ARC_POINTS := 40
const TRIM := 0.34                   # a lane stops short of the harbor it leaves
const FADE := 0.4                    # length over which a lane's ends fade
const LIFT := 0.03                   # the line floats this far over the water
const WATERLINE := -0.02             # a ship sits this far under its origin so the hull is in the water
const TOKEN_SCALE := 0.75
const TOKEN_SPEED := 0.22            # world units per second
const WRECK_SCALE := 1.3

var b                                # the M7WorldBuilder
var holder: Node3D
var built: Array = []
var materials: Array = []
var lanes: Array = []                # {pts, cum, length, civ, volume, age}
var tokens: Array = []               # {lane, phase, dir, kind}
var wrecks: Array = []               # {node, base}: the wreck meshes and where they float, scaled with the camera like the tokens
var token_mm := {}                   # kind -> MultiMeshInstance3D
var sea_y := 0.0
var demo := false                    # the gallery drives the tokens from the wall clock, the game from the render tick
var signature := -1
var boost := 1.0

func _init(builder, parent: Node3D) -> void:
	b = builder
	holder = Node3D.new()
	holder.name = "OceanLanes"
	parent.add_child(holder)

func _clear() -> void:
	for n in built:
		if is_instance_valid(n):
			(n as Node).queue_free()
	built.clear()
	materials.clear()
	lanes.clear()
	tokens.clear()
	wrecks.clear()
	token_mm.clear()

static func _length(pts: PackedVector2Array) -> float:
	var total := 0.0
	for i in pts.size() - 1:
		total += pts[i].distance_to(pts[i + 1])
	return total

## The arc between two harbors: a straight run bowed sideways by `bulge` world units at the middle (a sailed lane curves, it is never ruled).
static func arc(from: Vector2, to: Vector2, bulge: float) -> PackedVector2Array:
	var pts := PackedVector2Array()
	var d := to - from
	var perp := Vector2(-d.y, d.x).normalized()
	for i in ARC_POINTS + 1:
		var u := float(i) / ARC_POINTS
		pts.append(from.lerp(to, u) + perp * bulge * sin(PI * u))
	return pts

## The point `d` along the polyline and the direction it runs there.
static func _at(lane: Dictionary, d: float) -> Array:
	var pts: PackedVector2Array = lane.pts
	var cum: PackedFloat32Array = lane.cum
	d = clampf(d, 0.0, float(lane.length))
	for i in range(1, pts.size()):
		if cum[i] >= d or i == pts.size() - 1:
			var seg := maxf(cum[i] - cum[i - 1], 0.0001)
			var t := clampf((d - cum[i - 1]) / seg, 0.0, 1.0)
			return [pts[i - 1].lerp(pts[i], t), (pts[i] - pts[i - 1]).normalized()]
	return [pts[0], Vector2.RIGHT]

func _route_material(mode: int, dash: float) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = load(ROUTE_SHADER)
	m.set_shader_parameter("mode", mode)
	m.set_shader_parameter("dash", dash)
	m.set_shader_parameter("width_boost", boost)
	materials.append(m)
	return m

func _multimesh(mesh_name: String, count: int) -> MultiMeshInstance3D:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_custom_data = true
	mm.mesh = b.kit.get_mesh(mesh_name)
	mm.instance_count = count
	mm.visible_instance_count = 0
	var inst := MultiMeshInstance3D.new()
	inst.name = mesh_name
	inst.multimesh = mm
	inst.material_override = b.mats.prop
	inst.extra_cull_margin = 3.0
	holder.add_child(inst)
	built.append(inst)
	return inst

## Rebuilds the overlay for the given lists. `lane_list` entries: {from: Vector2, to: Vector2, civ: int, volume: 0..1, age: 0..2, bulge: float}; `token_list`:
## {lane: int, phase: 0..1, dir: 1 or -1, kind: "caravel" or "galleon"}; `wreck_list`: {pos: Vector2, seed: int}. `water_y` is the sea's surface (0 on the real map).
func sync(lane_list: Array, token_list: Array, wreck_list: Array, water_y: float = 0.0) -> void:
	var sig := hash([lane_list, token_list, wreck_list, water_y])   # every field of every entry, so any change rebuilds
	if sig == signature:
		return
	signature = sig
	_clear()
	sea_y = water_y
	if not lane_list.is_empty():
		b.kit.ensure_discovery()
	if lane_list.is_empty():
		return
	var line := {"V": PackedVector3Array(), "C": PackedColorArray(), "U": PackedVector2Array(), "U2": PackedVector2Array(), "I": PackedInt32Array()}
	for l in lane_list:
		var pts := arc(l.from, l.to, float(l.get("bulge", 0.8)))
		var cum := PackedFloat32Array([0.0])
		for i in range(1, pts.size()):
			cum.append(cum[i - 1] + pts[i].distance_to(pts[i - 1]))
		var lane := {"pts": pts, "cum": cum, "length": cum[cum.size() - 1], "civ": int(l.civ), "volume": float(l.volume), "age": int(l.age)}
		lanes.append(lane)
		# a lane's thickness is its volume (a thin line for a trickle, a bold one for a busy lane); it fades over two idle years
		var half := 0.012 + 0.07 * clampf(float(l.volume), 0.0, 1.0)
		var alpha: float = [0.85, 0.35, 0.0][clampi(int(l.age), 0, 2)]
		var col := M7WorldData.civ_color(int(l.civ)).lerp(Color.WHITE, 0.45)
		_ribbon(line, lane, half, col, alpha)
	var mesh := ArrayMesh.new()
	if (line.V as PackedVector3Array).size() > 0:
		var arr := []
		arr.resize(Mesh.ARRAY_MAX)
		arr[Mesh.ARRAY_VERTEX] = line.V
		arr[Mesh.ARRAY_COLOR] = line.C
		arr[Mesh.ARRAY_TEX_UV] = line.U
		arr[Mesh.ARRAY_TEX_UV2] = line.U2
		arr[Mesh.ARRAY_INDEX] = line.I
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
		var node := MeshInstance3D.new()
		node.name = "Lanes"
		node.mesh = mesh
		node.material_override = _route_material(0, 0.2)
		node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		holder.add_child(node)
		built.append(node)
	# the tokens: one multimesh per ship kind, filled each update
	var counts := {}
	for t in token_list:
		var lane_i := int(t.lane)
		if lane_i < 0 or lane_i >= lanes.size() or int(lanes[lane_i].age) >= 2:
			continue
		tokens.append(t)
		counts[String(t.kind)] = int(counts.get(String(t.kind), 0)) + 1
	for kind in counts:
		token_mm[kind] = _multimesh(String(kind), int(counts[kind]))
	# the wrecks: a floating mesh each, bobbing a little (placed once; the bob is in update)
	for wr in wreck_list:
		var node2 := MeshInstance3D.new()
		node2.name = "Wreck"
		node2.mesh = b.kit.get_mesh("wreck")
		node2.material_override = b.mats.prop
		var p: Vector2 = wr.pos
		var base := Transform3D(Basis(Vector3.UP, float(int(wr.get("seed", 0)) % 628) * 0.01), Vector3(p.x, sea_y + WATERLINE * 0.4, p.y))
		node2.transform = base.scaled_local(Vector3.ONE * WRECK_SCALE * (1.0 + (boost - 1.0) * 0.8))
		node2.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		holder.add_child(node2)
		built.append(node2)
		wrecks.append({"node": node2, "base": base})

## One dashed ribbon along a lane, trimmed at the harbors and fading at its ends and with the lane's age.
func _ribbon(a: Dictionary, lane: Dictionary, half: float, col: Color, alpha: float) -> void:
	if alpha <= 0.0 or float(lane.length) < 2.0 * TRIM + 0.4:
		return
	var pts: PackedVector2Array = lane.pts
	var cum: PackedFloat32Array = lane.cum
	var from_d := TRIM
	var to_d := float(lane.length) - TRIM
	var base: int = a.V.size()
	var rows := 0
	for i in pts.size():
		var acc := cum[i]
		if acc < from_d or acc > to_d:
			continue
		var p0 := pts[maxi(i - 1, 0)]
		var p1 := pts[mini(i + 1, pts.size() - 1)]
		var dir := (p1 - p0).normalized() if p0 != p1 else Vector2.RIGHT
		var nrm := Vector2(-dir.y, dir.x)
		var fade := smoothstep(0.0, 1.0, minf(clampf((acc - from_d) / FADE, 0.0, 1.0), clampf((to_d - acc) / FADE, 0.0, 1.0)))
		for k in 2:
			var side: float = -1.0 if k == 0 else 1.0
			var q: Vector2 = pts[i] + nrm * half * side
			a.V.append(Vector3(q.x, sea_y + LIFT, q.y))
			a.U.append(Vector2(acc, side))
			a.U2.append(q - pts[i])
			var c := col.srgb_to_linear()
			c.a = alpha * fade
			a.C.append(c)
		rows += 1
	for r in rows - 1:
		var i0 := base + r * 2
		a.I.append(i0); a.I.append(i0 + 1); a.I.append(i0 + 2)
		a.I.append(i0 + 1); a.I.append(i0 + 3); a.I.append(i0 + 2)

## Moves the tokens for `seconds` of render time (the render tick * 0.1 in a match, the wall clock in the gallery): each sails its lane from one end to the other
## and starts again, a pure function of the clock so pause, speed and loading all work.
func update(seconds: float) -> void:
	if tokens.is_empty():
		return
	var index := {}
	for t in tokens:
		var kind := String(t.kind)
		var mm: MultiMeshInstance3D = token_mm[kind]
		var n := int(index.get(kind, 0))
		var lane: Dictionary = lanes[int(t.lane)]
		var run := float(lane.length) - 2.0 * TRIM
		if run < 0.3:
			continue
		var u := fposmod(float(t.phase) + seconds * TOKEN_SPEED / run, 1.0)
		var forward := int(t.dir) >= 0
		var s := TRIM + run * (u if forward else 1.0 - u)
		var here: Array = _at(lane, s)
		var pos: Vector2 = here[0]
		var dir: Vector2 = here[1] if forward else -(here[1] as Vector2)
		var bob := 0.004 * sin(seconds * 1.6 + float(t.phase) * 9.0)
		var grow := 1.0 + (boost - 1.0) * 0.55        # ships grow a little with the camera's distance so they still read at region zoom
		var xf := Transform3D(Basis(Vector3.UP, atan2(dir.x, dir.y)).scaled(Vector3.ONE * TOKEN_SCALE * grow), Vector3(pos.x, sea_y + WATERLINE + bob, pos.y))
		mm.multimesh.set_instance_transform(n, xf)
		var tint := M7MeshBuilder.lin(M7WorldData.civ_color(int(lane.civ)), 0.5)
		mm.multimesh.set_instance_custom_data(n, Color(tint.r, tint.g, tint.b, 0.5))
		index[kind] = n + 1
	for kind in token_mm:
		(token_mm[kind] as MultiMeshInstance3D).multimesh.visible_instance_count = int(index.get(kind, 0))

## Lines widen with the camera's distance, like the trails, so they are still there at region zoom.
func set_boost(value: float) -> void:
	boost = value
	for m in materials:
		(m as ShaderMaterial).set_shader_parameter("width_boost", value)
	for wr in wrecks:
		(wr.node as MeshInstance3D).transform = (wr.base as Transform3D).scaled_local(Vector3.ONE * WRECK_SCALE * (1.0 + (value - 1.0) * 0.8))

## The gallery stage's lanes: a busy lane with a caravel each way, a half-idle lane with a galleon escorting a caravel and a wreck, and a lane gone quiet for a
## year. `at` is the stage's centre in world space (the stage floats a little above the map, so the sea is its floor).
func load_demo(at: Vector3) -> void:
	demo = true
	var o := Vector2(at.x, at.z)
	var lane_list := [
		{"from": o + Vector2(-4.4, -33.0), "to": o + Vector2(4.2, -35.6), "civ": 0, "volume": 0.95, "age": 0, "bulge": 1.3},
		{"from": o + Vector2(-4.0, -36.4), "to": o + Vector2(3.8, -32.8), "civ": 1, "volume": 0.4, "age": 0, "bulge": -1.1},
		{"from": o + Vector2(-4.6, -31.6), "to": o + Vector2(-1.0, -37.0), "civ": 2, "volume": 0.2, "age": 1, "bulge": 0.7},
	]
	var token_list := [
		{"lane": 0, "phase": 0.1, "dir": 1, "kind": "caravel"},
		{"lane": 0, "phase": 0.55, "dir": -1, "kind": "caravel"},
		{"lane": 1, "phase": 0.3, "dir": 1, "kind": "galleon"},
		{"lane": 1, "phase": 0.38, "dir": 1, "kind": "caravel"},
		{"lane": 2, "phase": 0.5, "dir": -1, "kind": "caravel"},
	]
	var wreck_list := [{"pos": o + Vector2(0.6, -34.3), "seed": 7}]
	sync(lane_list, token_list, wreck_list, at.y + 0.02)
