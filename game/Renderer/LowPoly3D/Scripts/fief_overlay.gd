## Faith & Fief route and spread overlays (docs/MVP/32 §7 and §8.4): the pilgrimage routes, the quarantine on routes and borders, and disease spreading
## between settlements, drawn on the ground over the map.
##
## Everything here is driven by tables in M7WorldData that the frame will fill when the E5 mode exists (docs/MVP/32 §9: `pilgrimRoutes`, `quarantine`,
## and the outbreak and spread lists; the names and shapes are proposals). With no data nothing is built and nothing is drawn, so Bronze and Iron
## are untouched. `load_demo` fills the tables from the live map for the gallery (scenario `fief-overlays`), so the look can be judged before the sim
## has the data.
##
##   pilgrimage route   a thin dashed line of pale gold on the road, a way-post with a scallop badge at each end (arm pointing along the route), and pilgrims
##                      walking it to and from; a route that ends at the Cathedral gets a soft gold edge under the line (docs/MVP/32 §7)
##   quarantine         a hazard-striped line along the route or border where quarantine is in force, with a barrier gate across it in the middle
##   spread             a sickly green line along the route or border the disease travels, with pulses running from the source settlement toward the target
##   outbreak           a slowly turning yellow-green stain on the ground round a settlement where disease is raging, stronger as it worsens
extends RefCounted

const ROUTE_SHADER := "res://Renderer/LowPoly3D/Shaders/route_line.gdshader"
const OUTBREAK_SHADER := "res://Renderer/LowPoly3D/Shaders/outbreak.gdshader"

const TRIM := 0.42                  # a line stops short of the settlement it leaves
const FADE := 0.32                  # length over which a line's ends fade
const LIFT := 0.018
const WALK_SPEED := 0.1             # world units per second
const WALK_DWELL := 6.0             # seconds a pilgrim rests at the end of the route
const WALKER_SIZE := 1.25
const MAX_WALKERS := 30
const MARKER_SIZE := 1.8
const GATE_SIZE := 2.3

const PILGRIM_COL := Color("#f4d77f")
const EDGE_COL := Color("#d8a735")
const QUARANTINE_COL := Color("#e6c230")
const SPREAD_COL := Color("#dbe63e")
const SPREAD_UNDER := Color("#3d4a0e")
const OUTBREAK_COL := Color("#c3d12a")
const IMPORTED_COL := Color("#c9a02a")

var b                                   # the M7WorldBuilder
var holder: Node3D
var built: Array = []                   # every node of the last build
var materials: Array = []               # the route materials, for the camera's width boost
var walkers: Array = []                 # {pts, cum, length, civ, phase, id}
var walker_mm: MultiMeshInstance3D
var shadow_mm: MultiMeshInstance3D
var signature := -1
var last_tick := -1.0
var boost := 1.0
var focus := Vector3.ZERO               # where the demo put its first route (the origin when none)
var gate_focus := Vector3.ZERO          # where the first quarantine gate stands (the origin when none)

func _init(builder, parent: Node3D) -> void:
	b = builder
	holder = Node3D.new()
	holder.name = "FiefOverlay"
	parent.add_child(holder)

# ------------------------------------------------------------------ building

func _clear() -> void:
	for n in built:
		if is_instance_valid(n):
			(n as Node).queue_free()
	built.clear()
	materials.clear()
	walkers.clear()
	walker_mm = null
	shadow_mm = null
	gate_focus = Vector3.ZERO

## Rebuilds the overlay when the tables change (or with `force`).
func sync(force: bool = false) -> void:
	var w: M7WorldData = b.w
	var sig := hash(w.pilgrim_routes) * 31 + hash(w.pilgrim_tiles) + hash(w.quarantine_links) * 17 + hash(w.quarantine_tiles) * 3 \
		+ hash(w.spread_links) * 13 + hash(w.spread_tiles) * 7 + hash(w.outbreaks) * 5
	if sig == signature and not force:
		return
	signature = sig
	_clear()
	last_tick = -1.0
	if w.pilgrim_routes.is_empty() and w.quarantine_links.is_empty() and w.spread_links.is_empty() and w.outbreaks.is_empty():
		return
	_build_pilgrim(w)
	_build_quarantine(w)
	_build_spread(w)
	_build_outbreaks(w)

static func _arrays() -> Dictionary:
	return {"V": PackedVector3Array(), "C": PackedColorArray(), "U": PackedVector2Array(), "U2": PackedVector2Array(), "I": PackedInt32Array()}

static func _length(pts: PackedVector2Array) -> float:
	var total := 0.0
	for i in pts.size() - 1:
		total += pts[i].distance_to(pts[i + 1])
	return total

## The tiles of one run of a table.
static func _run(tiles: PackedInt32Array, start: int, length: int) -> PackedInt32Array:
	var run := PackedInt32Array()
	for j in length:
		if start + j < tiles.size() and tiles[start + j] >= 0:
			run.append(tiles[start + j])
	return run

## One ribbon along `pts`, `half` wide each side of the line, between the distances trim_a and (length - trim_b), fading in and out at its ends.
func _ribbon(a: Dictionary, pts: PackedVector2Array, half: float, col: Color, alpha: float, trim_a: float, trim_b: float, lift: float) -> void:
	var w: M7WorldData = b.w
	var total := _length(pts)
	var from_d := trim_a
	var to_d := total - trim_b
	if to_d - from_d < 0.2:
		return
	var base: int = a.V.size()
	var rows := 0
	var acc := 0.0
	for i in pts.size():
		if i > 0:
			acc += pts[i].distance_to(pts[i - 1])
		if acc < from_d or acc > to_d:
			continue
		var p0 := pts[maxi(i - 1, 0)]
		var p1 := pts[mini(i + 1, pts.size() - 1)]
		var dir := (p1 - p0).normalized() if p0 != p1 else Vector2.RIGHT
		var nrm := Vector2(-dir.y, dir.x)
		var fade := smoothstep(0.0, 1.0, minf(clampf((acc - from_d) / FADE, 0.0, 1.0), clampf((to_d - acc) / FADE, 0.0, 1.0)))
		var ps: Array = []
		var hs := 0.0
		for side in [-1.0, 1.0]:
			var p: Vector2 = pts[i] + nrm * half * side
			ps.append(p)
			hs = maxf(hs, maxf(w.height_at(p), 0.0))
		for k in 2:
			var side: float = -1.0 if k == 0 else 1.0
			var q: Vector2 = ps[k]
			a.V.append(Vector3(q.x, hs + lift, q.y))
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

func _mesh(a: Dictionary) -> ArrayMesh:
	var mesh := ArrayMesh.new()
	if (a.V as PackedVector3Array).size() == 0:
		return mesh
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = a.V
	arr[Mesh.ARRAY_COLOR] = a.C
	arr[Mesh.ARRAY_TEX_UV] = a.U
	arr[Mesh.ARRAY_TEX_UV2] = a.U2
	arr[Mesh.ARRAY_INDEX] = a.I
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	return mesh

func _route_material(mode: int, dash: float = 0.14, speed: float = 0.5) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = load(ROUTE_SHADER)
	m.set_shader_parameter("mode", mode)
	m.set_shader_parameter("dash", dash)
	m.set_shader_parameter("speed", speed)
	m.set_shader_parameter("width_boost", boost)
	materials.append(m)
	return m

func _add_mesh(name: String, a: Dictionary, material: Material) -> void:
	var mesh := _mesh(a)
	if mesh.get_surface_count() == 0:
		return
	var node := MeshInstance3D.new()
	node.name = name
	node.mesh = mesh
	node.material_override = material
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	holder.add_child(node)
	built.append(node)

## The point `d` along a smoothed line and the direction the line runs there.
static func _at(pts: PackedVector2Array, d: float) -> Array:
	var acc := 0.0
	for i in range(1, pts.size()):
		var seg := pts[i].distance_to(pts[i - 1])
		if acc + seg >= d or i == pts.size() - 1:
			var t := clampf((d - acc) / maxf(seg, 0.0001), 0.0, 1.0)
			return [pts[i - 1].lerp(pts[i], t), (pts[i] - pts[i - 1]).normalized()]
		acc += seg
	return [pts[0], Vector2.RIGHT]

static func _face(dir: Vector2) -> float:
	return atan2(-dir.y, dir.x)

func _props_xf(p: Vector2, dir: Vector2, scale: float) -> Transform3D:
	var w: M7WorldData = b.w
	return Transform3D(Basis(Vector3.UP, _face(dir)).scaled(Vector3.ONE * scale), Vector3(p.x, maxf(w.height_at(p), 0.0), p.y))

## Pilgrimage routes: the dashed line (and the gold edge under the ones that end at the Cathedral), a way-post at each end, pilgrims walking.
func _build_pilgrim(w: M7WorldData) -> void:
	var line := _arrays()
	var edge := _arrays()
	var posts := M7Batcher.new()
	posts.chunk_size = 100.0
	for r in range(0, w.pilgrim_routes.size(), 5):
		var run := _run(w.pilgrim_tiles, w.pilgrim_routes[r + 2], w.pilgrim_routes[r + 3])
		if run.size() < 2:
			continue
		var civ := w.pilgrim_routes[r + 1]
		var cathedral := (w.pilgrim_routes[r + 4] & 1) != 0
		var pts := M7TrailBuilder._smooth(w, run)
		var total := _length(pts)
		_ribbon(line, pts, 0.02, PILGRIM_COL, 0.95, TRIM * 0.7, TRIM * 0.7, LIFT)
		if cathedral:
			_ribbon(edge, pts, 0.052, EDGE_COL, 0.5, TRIM * 0.7, TRIM * 0.7, LIFT - 0.006)
		# a way-post at each end, its arm along the route and a little in from the settlement
		for end in 2:
			var d := 0.5 if end == 0 else total - 0.5
			var here := _at(pts, d)
			var dir: Vector2 = here[1] if end == 0 else -(here[1] as Vector2)
			var off := dir.rotated(PI * 0.5) * 0.09
			posts.add("pilgrim_marker", _props_xf(here[0] + off, dir, MARKER_SIZE), Color(1, 1, 1, 0.5), false)
		# pilgrims on the road: two on a route, three on a Cathedral route
		var cum := PackedFloat32Array()
		var acc := 0.0
		cum.append(0.0)
		for i in range(1, pts.size()):
			acc += pts[i].distance_to(pts[i - 1])
			cum.append(acc)
		for k in (3 if cathedral else 2):
			if walkers.size() >= MAX_WALKERS:
				break
			walkers.append({"pts": pts, "cum": cum, "length": total, "civ": civ, "id": walkers.size(), "tile": run[0],
				"phase": fmod(M7WorldData.hash01(run[0], w.pilgrim_routes[r], 811) + float(k) / 3.0, 1.0)})
	_add_mesh("PilgrimEdge", edge, _route_material(3))
	_add_mesh("PilgrimLine", line, _route_material(0, 0.13))
	if posts.groups.size() > 0:
		posts.build(holder, b.kit, Callable(b, "_material_for"))
		for child in holder.get_children():
			if not built.has(child) and (child as Node).name.begins_with("pilgrim_marker"):
				built.append(child)
	_make_walkers()

## The quarantine: hazard stripes along the route or border, and a barrier gate across it where it runs through the middle.
func _build_quarantine(w: M7WorldData) -> void:
	var line := _arrays()
	var gates := M7Batcher.new()
	gates.chunk_size = 100.0
	for r in range(0, w.quarantine_links.size(), 3):
		var run := _run(w.quarantine_tiles, w.quarantine_links[r + 1], w.quarantine_links[r + 2])
		if run.size() < 2:
			continue
		var pts := M7TrailBuilder._smooth(w, run)
		var total := _length(pts)
		_ribbon(line, pts, 0.034, QUARANTINE_COL, 0.95, TRIM, TRIM, LIFT + 0.002)
		var mid := _at(pts, total * 0.5)
		if gate_focus == Vector3.ZERO:
			gate_focus = Vector3(mid[0].x, maxf(w.height_at(mid[0]), 0.0), mid[0].y)
		gates.add("quarantine_gate", _props_xf(mid[0], mid[1], GATE_SIZE), Color(1, 1, 1, 0.5), false)
	_add_mesh("QuarantineLine", line, _route_material(2, 0.1))
	if gates.groups.size() > 0:
		gates.build(holder, b.kit, Callable(b, "_material_for"))
		for child in holder.get_children():
			if not built.has(child) and (child as Node).name.begins_with("quarantine_gate"):
				built.append(child)

## Spread: a sickly line along the way the disease travels with pulses running from the source toward the target.
func _build_spread(w: M7WorldData) -> void:
	var line := _arrays()
	var under := _arrays()
	for r in range(0, w.spread_links.size(), 3):
		var run := _run(w.spread_tiles, w.spread_links[r], w.spread_links[r + 1])
		if run.size() < 2:
			continue
		var strength := clampf(float(w.spread_links[r + 2]) / 1000.0, 0.25, 1.0)
		var pts := M7TrailBuilder._smooth(w, run)
		# a dark olive underlay so the pulses read on any ground, then the pulses themselves
		_ribbon(under, pts, 0.05, SPREAD_UNDER, 0.55, TRIM * 0.8, TRIM * 0.8, LIFT + 0.002)
		_ribbon(line, pts, 0.036, SPREAD_COL, 0.7 + 0.3 * strength, TRIM * 0.8, TRIM * 0.8, LIFT + 0.005)
	_add_mesh("SpreadUnder", under, _route_material(3))
	_add_mesh("SpreadLine", line, _route_material(1, 0.12, 0.45))

## Outbreaks: a draped stain on the ground round each settlement where disease is raging, larger and stronger as it worsens.
func _build_outbreaks(w: M7WorldData) -> void:
	var a := _arrays()
	for r in range(0, w.outbreaks.size(), 3):
		var tile := w.outbreaks[r]
		if tile < 0 or tile >= w.width * w.height:
			continue
		var strength := clampf(float(w.outbreaks[r + 1]) / 1000.0, 0.2, 1.0)
		var imported := (w.outbreaks[r + 2] & 1) != 0
		var centre := w.tile_center(tile)
		_disc(a, centre, 0.55 + 0.55 * strength, IMPORTED_COL if imported else OUTBREAK_COL, 0.55 + 0.4 * strength)
	var m := ShaderMaterial.new()
	m.shader = load(OUTBREAK_SHADER)
	_add_mesh("Outbreaks", a, m)

## A disc draped on the terrain: a centre vertex and rings of vertices, UV running -1..1.
func _disc(a: Dictionary, centre: Vector2, radius: float, col: Color, alpha: float) -> void:
	var w: M7WorldData = b.w
	var rings := 6
	var segs := 28
	var base: int = a.V.size()
	var c := col.srgb_to_linear()
	c.a = alpha
	a.V.append(Vector3(centre.x, maxf(w.height_at(centre), 0.0) + 0.03, centre.y))
	a.U.append(Vector2.ZERO)
	a.U2.append(Vector2.ZERO)
	a.C.append(c)
	for ring in range(1, rings + 1):
		var rr := float(ring) / rings
		for s in segs:
			var ang := TAU * s / segs
			var d := Vector2(cos(ang), sin(ang))
			var p := centre + d * radius * rr
			a.V.append(Vector3(p.x, maxf(w.height_at(p), 0.0) + 0.03, p.y))
			a.U.append(d * rr)
			a.U2.append(Vector2.ZERO)
			a.C.append(c)
	for s in segs:
		var s1 := (s + 1) % segs
		a.I.append(base); a.I.append(base + 1 + s); a.I.append(base + 1 + s1)
	for ring in range(1, rings):
		var o0 := base + 1 + (ring - 1) * segs
		var o1 := base + 1 + ring * segs
		for s in segs:
			var s1 := (s + 1) % segs
			a.I.append(o0 + s); a.I.append(o1 + s); a.I.append(o1 + s1)
			a.I.append(o0 + s); a.I.append(o1 + s1); a.I.append(o0 + s1)

# ------------------------------------------------------------------ pilgrims

func _make_walkers() -> void:
	if walkers.is_empty():
		return
	walker_mm = _multimesh("person_pilgrim", b.mats.figure, walkers.size())
	shadow_mm = _multimesh("shadow_disc", b.mats.shadow_disc, walkers.size())

func _multimesh(mesh_name: String, material: Material, count: int) -> MultiMeshInstance3D:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_custom_data = true
	mm.mesh = b.kit.get_mesh(mesh_name)
	mm.instance_count = count
	mm.visible_instance_count = 0
	var inst := MultiMeshInstance3D.new()
	inst.name = mesh_name
	inst.multimesh = mm
	inst.material_override = material
	inst.extra_cull_margin = 2.5
	holder.add_child(inst)
	built.append(inst)
	return inst

## Pilgrims walk the route end to end and back, resting at each end: a pure function of the tick, so pause, speed and loading all work.
func update(tick: float) -> void:
	if walker_mm == null or tick == last_tick:
		return
	last_tick = tick
	var w: M7WorldData = b.w
	var seconds := tick * 0.1
	var n := 0
	for walker in walkers:
		var length: float = walker.length
		var run := length - 2.0 * TRIM
		if run < 0.3:
			continue
		var leg := run / WALK_SPEED
		var period := 2.0 * (leg + WALK_DWELL)
		var tt := fposmod(seconds + float(walker.phase) * period, period)
		var s := TRIM
		var forward := true
		var walking := true
		if tt < leg:
			s = TRIM + run * tt / leg
		elif tt < leg + WALK_DWELL:
			s = length - TRIM
			walking = false
		elif tt < 2.0 * leg + WALK_DWELL:
			s = length - TRIM - run * (tt - leg - WALK_DWELL) / leg
			forward = false
		else:
			walking = false
			forward = false
		var here: Array = M7DogRig._along(walker, s)
		var pos: Vector2 = here[0]
		# pilgrims keep to one side of the road, so the two directions pass each other
		var dir: Vector2 = here[1] if forward else -(here[1] as Vector2)
		pos += dir.rotated(PI * 0.5) * 0.03
		var y := maxf(w.height_at(pos), 0.0) + (0.0035 * absf(sin(s * 55.0)) if walking else 0.0)
		var xf := Transform3D(Basis(Vector3.UP, atan2(dir.x, dir.y)).scaled(Vector3.ONE * WALKER_SIZE), Vector3(pos.x, y, pos.y))
		walker_mm.multimesh.set_instance_transform(n, xf)
		walker_mm.multimesh.set_instance_custom_data(n, Color(float(int(walker.civ) % 8) + M7WorldData.hash01(int(walker.tile), int(walker.id), 812) * 0.98, 0.0, 0.0, 0.0))
		shadow_mm.multimesh.set_instance_transform(n, Transform3D(Basis(Vector3.UP, 0.0).scaled(Vector3(1.4, 1.0, 1.4)), Vector3(pos.x, y + 0.001, pos.y)))
		shadow_mm.multimesh.set_instance_custom_data(n, Color(0, 0, 0, 0))
		n += 1
	walker_mm.multimesh.visible_instance_count = n
	shadow_mm.multimesh.visible_instance_count = n

## Lines widen with the camera's distance, like the trails, so they are still there at region zoom.
func set_boost(value: float) -> void:
	boost = value
	for m in materials:
		(m as ShaderMaterial).set_shader_parameter("width_boost", value)

# ------------------------------------------------------------------ the gallery demo

## Fills the tables from the live map round the first settlement of the human civilization: two pilgrimage routes (one ending at a Cathedral), a
## quarantined route and border, disease spreading outward from the capital along a route, and an outbreak at each end of it. Returns where to look.
func load_demo() -> Vector3:
	var w: M7WorldData = b.w
	var home := -1
	for s in w.settlements:
		if int(s.get("tile", -1)) >= 0 and int(s.get("civ", -1)) == 0:
			home = int(s.get("tile", -1))
			break
	if home < 0:
		for s in w.settlements:
			if int(s.get("tile", -1)) >= 0:
				home = int(s.get("tile", -1))
				break
	if home < 0:
		return Vector3.ZERO
	var centre := w.tile_center(home)
	var runs: Array = []
	for angle in [0.15, 1.3, 2.35, 3.5, 4.55, 5.5]:
		var dir := Vector2(cos(angle), sin(angle))
		var run := PackedInt32Array()
		for k in range(0, 18):
			var t := w.tile_at(centre + dir * (0.5 + 0.9 * k))
			if t < 0 or not w.is_land(t):
				break
			if run.is_empty() or run[run.size() - 1] != t:
				run.append(t)
		if run.size() >= 7:
			runs.append(run)
	w.pilgrim_routes = PackedInt32Array()
	w.pilgrim_tiles = PackedInt32Array()
	w.quarantine_links = PackedInt32Array()
	w.quarantine_tiles = PackedInt32Array()
	w.spread_links = PackedInt32Array()
	w.spread_tiles = PackedInt32Array()
	w.outbreaks = PackedInt32Array()
	var roles := ["pilgrim_cathedral", "pilgrim", "quarantine_route", "spread", "quarantine_border", "pilgrim"]
	for i in runs.size():
		var run: PackedInt32Array = runs[i]
		match roles[i]:
			"pilgrim_cathedral", "pilgrim":
				var start := w.pilgrim_tiles.size()
				w.pilgrim_tiles.append_array(run)
				w.pilgrim_routes.append_array(PackedInt32Array([i, 0, start, run.size(), 1 if roles[i] == "pilgrim_cathedral" else 0]))
			"quarantine_route", "quarantine_border":
				var qstart := w.quarantine_tiles.size()
				w.quarantine_tiles.append_array(run)
				w.quarantine_links.append_array(PackedInt32Array([0 if roles[i] == "quarantine_route" else 1, qstart, run.size()]))
			"spread":
				var sstart := w.spread_tiles.size()
				w.spread_tiles.append_array(run)
				w.spread_links.append_array(PackedInt32Array([sstart, run.size(), 750]))
				w.outbreaks.append_array(PackedInt32Array([run[run.size() - 1], 450, 1]))
	w.outbreaks.append_array(PackedInt32Array([home, 850, 0]))
	sync(true)
	focus = Vector3(centre.x, maxf(w.height_at(centre), 0.0), centre.y)
	return focus
