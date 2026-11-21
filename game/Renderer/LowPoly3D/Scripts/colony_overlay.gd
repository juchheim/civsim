## Discovery (E6) colonies (docs/MVP/39 §7.3): what marks a settlement as a colony, how it is faring, and the two moments that change it: founding and breaking away.
##
## A colony is a member settlement the capital cannot reach on foot (docs/MVP/38 WP-E6-6 slice 2), drawn as the ordinary settlement plus this overlay, fed by the Discovery
## frame's `colonyState` (docs/MVP/39 §9; the names and shapes are proposals) and handed to `sync`. With no data nothing is built, so Bronze, Iron and Fief are untouched.
## `load_demo` (the gallery) builds one colony in each state and a founding, so the look can be judged before the sim has the data.
##
##   content      the owner's pennant on a pole beside the Heart, and a faint dotted tether across the water to the capital; nothing more (a colony that is doing well
##                has no ring)
##   strained     the same with a dashed yellow ring on the ground round the settlement: its Cohesion is low from sea strain (the roll that can break it away may fire)
##   blockaded    the same with a solid red ring and, where the frame names one, a galleon on station at the harbor mouth: the harbor is under blockade, the strain is higher
##   independent  the colony has broken away (the seeded yearly roll hit, docs/MVP/38): the pennant is struck and a plain grey-green flag flies from a short pole, the
##                tether is gone, and a pale pulse ring spreads out from the settlement once (the standard Moment burst); the town is then an ordinary Free Folk settlement
##   founding     a new colony's first seasons: a caravel pulled up on the beach (the `caravel` mesh canted nose-up and half sunk), a group of settlers with bundles and a
##                spade, and the pennant planted beside them; shown for FOUNDING_SHOWN seconds
extends RefCounted

const ROUTE_SHADER := "res://Renderer/LowPoly3D/Shaders/route_line.gdshader"
const RING_R := 0.78                  # the state ring's radius round the settlement's Heart
const TETHER_TRIM := 0.5              # the tether stops short of both settlements
const FOUNDING_SHOWN := 10.0          # seconds a founding stays up
const PULSE_LEN := 3.0                # seconds a breaking-away pulse takes to spread
const STATE_CONTENT := 1
const STATE_STRAINED := 2
const STATE_BLOCKADED := 3
const STATE_INDEPENDENT := 4
const STRAIN_COL := Color("#e6c230")
const BLOCKADE_COL := Color("#d83a2e")

var b                                  # the M7WorldBuilder
var holder: Node3D
var built: Array = []
var materials: Array = []
var pulses: Array = []                 # {node, mat, pos, seed}
var flat_y := NAN                      # a fixed height for the demo stage; NAN on the real map (the terrain's height is used)
var demo := false
var signature := -1
var boost := 1.0
var clock := 0.0

func _init(builder, parent: Node3D) -> void:
	b = builder
	holder = Node3D.new()
	holder.name = "Colonies"
	parent.add_child(holder)

func _clear() -> void:
	for n in built:
		if is_instance_valid(n):
			(n as Node).queue_free()
	built.clear()
	materials.clear()
	pulses.clear()

func _y(p: Vector2) -> float:
	if not is_nan(flat_y):
		return flat_y
	return maxf((b.w as M7WorldData).height_at(p), 0.0)

func _route_material(mode: int, dash: float) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = load(ROUTE_SHADER)
	m.set_shader_parameter("mode", mode)
	m.set_shader_parameter("dash", dash)
	m.set_shader_parameter("width_boost", boost)
	materials.append(m)
	return m

func _mm(mesh_name: String, entries: Array) -> void:
	if entries.is_empty():
		return
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_custom_data = true
	mm.mesh = b.kit.get_mesh(mesh_name)
	mm.instance_count = entries.size()
	for i in entries.size():
		mm.set_instance_transform(i, entries[i].xf)
		var tint := M7MeshBuilder.lin(M7WorldData.civ_color(int(entries[i].civ)), 0.5)
		mm.set_instance_custom_data(i, Color(tint.r, tint.g, tint.b, 0.5))
	var inst := MultiMeshInstance3D.new()
	inst.name = mesh_name
	inst.multimesh = mm
	inst.material_override = b.mats.prop
	inst.extra_cull_margin = 2.0
	holder.add_child(inst)
	built.append(inst)

static func _arrays() -> Dictionary:
	return {"V": PackedVector3Array(), "C": PackedColorArray(), "U": PackedVector2Array(), "U2": PackedVector2Array(), "I": PackedInt32Array()}

## A ribbon along a polyline of points (with their heights), `half` wide, in colour `col` (alpha included), with dash coordinates for the route shader.
func _strip(a: Dictionary, pts: PackedVector2Array, half: float, col: Color, trim_a: float, trim_b: float, closed: bool) -> void:
	var total := 0.0
	var cum := PackedFloat32Array([0.0])
	for i in range(1, pts.size()):
		total += pts[i].distance_to(pts[i - 1])
		cum.append(total)
	var base: int = a.V.size()
	var rows := 0
	for i in pts.size():
		var acc := cum[i]
		if acc < trim_a or acc > total - trim_b:
			continue
		var p0 := pts[maxi(i - 1, 0)]
		var p1 := pts[mini(i + 1, pts.size() - 1)]
		var dir := (p1 - p0).normalized() if p0 != p1 else Vector2.RIGHT
		var nrm := Vector2(-dir.y, dir.x)
		var fade := 1.0
		if not closed:
			fade = smoothstep(0.0, 1.0, minf(clampf((acc - trim_a) / 0.4, 0.0, 1.0), clampf((total - trim_b - acc) / 0.4, 0.0, 1.0)))
		for k in 2:
			var side: float = -1.0 if k == 0 else 1.0
			var q: Vector2 = pts[i] + nrm * half * side
			a.V.append(Vector3(q.x, _y(pts[i]) + 0.03, q.y))
			a.U.append(Vector2(acc, side))
			a.U2.append(q - pts[i])
			var c := col.srgb_to_linear()
			c.a = col.a * fade
			a.C.append(c)
		rows += 1
	for r in rows - 1:
		var i0 := base + r * 2
		a.I.append(i0); a.I.append(i0 + 1); a.I.append(i0 + 2)
		a.I.append(i0 + 1); a.I.append(i0 + 3); a.I.append(i0 + 2)

func _add_strip_mesh(name: String, a: Dictionary, material: Material) -> void:
	if (a.V as PackedVector3Array).size() == 0:
		return
	var mesh := ArrayMesh.new()
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = a.V
	arr[Mesh.ARRAY_COLOR] = a.C
	arr[Mesh.ARRAY_TEX_UV] = a.U
	arr[Mesh.ARRAY_TEX_UV2] = a.U2
	arr[Mesh.ARRAY_INDEX] = a.I
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	var node := MeshInstance3D.new()
	node.name = name
	node.mesh = mesh
	node.material_override = material
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	holder.add_child(node)
	built.append(node)

static func _circle(c: Vector2, r: float) -> PackedVector2Array:
	var pts := PackedVector2Array()
	for i in 51:                            # two points past the full turn, so the ring overlaps itself at the seam and closes
		var a := TAU * i / 48.0
		pts.append(c + Vector2(cos(a), sin(a)) * r)
	return pts

## Rebuilds the overlay. `colonies` entries: {pos: Vector2, capital: Vector2 (or the same as pos for none), civ: int, state: 1..4, ship: Vector2 (optional, a galleon on station)};
## `foundings`: {pos: Vector2, yaw: float, civ: int} (the frame lists the ones still young). `ground_y` is a fixed height for the demo stage (NAN on the real map).
func sync(colonies: Array, foundings: Array, ground_y: float = NAN) -> void:
	var sig := 29
	for c in colonies:
		sig = sig * 31 + int(float((c.pos as Vector2).x) * 100.0) + int(float((c.pos as Vector2).y) * 77.0) + int(c.state) * 11
	for f in foundings:
		sig = sig * 31 + int(float((f.pos as Vector2).x) * 100.0) + 3
	if sig == signature and (is_nan(ground_y) == is_nan(flat_y)):
		return
	signature = sig
	flat_y = ground_y
	_clear()
	if colonies.is_empty() and foundings.is_empty():
		return
	var pennants := []
	var flags := []
	var ships := []
	var rings_strain := _arrays()
	var rings_block := _arrays()
	var tethers := _arrays()
	for c in colonies:
		var p: Vector2 = c.pos
		var st := int(c.state)
		var civ := int(c.civ)
		var y := _y(p)
		if st == STATE_INDEPENDENT:
			flags.append({"xf": Transform3D(Basis.from_scale(Vector3.ONE * 1.4), Vector3(p.x + 0.34, y, p.y - 0.18)), "civ": civ})
			var pulse := MeshInstance3D.new()
			pulse.name = "BreakawayPulse"
			var torus := TorusMesh.new()
			torus.inner_radius = 0.96
			torus.outer_radius = 1.0
			torus.rings = 36
			torus.ring_segments = 3
			pulse.mesh = torus
			var mat := StandardMaterial3D.new()
			mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
			mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			mat.albedo_color = Color(1.0, 1.0, 1.0, 0.0)
			pulse.material_override = mat
			pulse.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			pulse.position = Vector3(p.x, y + 0.04, p.y)
			holder.add_child(pulse)
			built.append(pulse)
			pulses.append({"node": pulse, "mat": mat, "pos": p, "seed": int(float(p.x) * 31.0)})
			continue
		pennants.append({"xf": Transform3D(Basis.from_scale(Vector3.ONE * 1.5), Vector3(p.x + 0.34, y, p.y - 0.18)), "civ": civ})
		var tint := M7WorldData.civ_color(civ).lerp(Color.WHITE, 0.4)
		var cap: Vector2 = c.capital
		if cap.distance_to(p) > 2.0 * TETHER_TRIM + 0.6:
			var line := PackedVector2Array()
			for i in 33:
				var u := float(i) / 32.0
				line.append(p.lerp(cap, u))
			var tc := tint
			tc.a = 0.5
			_strip(tethers, line, 0.012, tc, TETHER_TRIM, TETHER_TRIM, false)
		if st == STATE_STRAINED:
			var rc := STRAIN_COL
			rc.a = 0.9
			_strip(rings_strain, _circle(p, RING_R), 0.028, rc, 0.0, 0.0, true)
		elif st == STATE_BLOCKADED:
			var bc := BLOCKADE_COL
			bc.a = 0.9
			_strip(rings_block, _circle(p, RING_R), 0.03, bc, 0.0, 0.0, true)
			if c.has("ship"):
				var sp: Vector2 = c.ship
				var toward := (sp - p).normalized()
				ships.append({"xf": Transform3D(Basis(Vector3.UP, atan2(-toward.x, -toward.y) + 0.0).scaled(Vector3.ONE * 0.8), Vector3(sp.x, _y(sp) - 0.02, sp.y)), "civ": (civ + 1) % 4})
	_add_strip_mesh("Tethers", tethers, _route_material(0, 0.07))
	_add_strip_mesh("StrainRings", rings_strain, _route_material(0, 0.1))
	_add_strip_mesh("BlockadeRings", rings_block, _route_material(3, 0.1))
	_mm("colony_pennant", pennants)
	_mm("colony_flag_free", flags)
	_mm("galleon", ships)
	# foundings: a caravel hauled up on the beach (canted nose-up and half sunk into the sand), the settlers, the planted pennant
	var beached := []
	var settlers := []
	for f in foundings:
		var fp: Vector2 = f.pos
		var yaw := float(f.get("yaw", 0.0))
		var fy := _y(fp)
		var hull := Basis(Vector3.UP, yaw) * Basis(Vector3.RIGHT, -0.22)
		beached.append({"xf": Transform3D(hull.scaled(Vector3.ONE * 1.3), Vector3(fp.x, fy - 0.03, fp.y)), "civ": int(f.civ)})
		var side := Vector2(cos(yaw), sin(yaw)).rotated(PI * 0.5)
		var sp2 := fp + side * 0.42
		settlers.append({"xf": Transform3D(Basis(Vector3.UP, yaw + PI * 0.5).scaled(Vector3.ONE * 1.4), Vector3(sp2.x, fy, sp2.y)), "civ": int(f.civ)})
	_mm("caravel", beached)
	_mm("colonist_group", settlers)

## Spreads the breaking-away pulses (a ring that grows from the settlement and fades, once every few seconds in the demo; in a match the frame's `colonyState` change
## starts it and it plays once) and nothing else: everything else is still. `seconds` is the render time (the render tick * 0.1; the wall clock in the demo).
func update(seconds: float) -> void:
	clock = seconds
	for pu in pulses:
		var t := fposmod(seconds + float(pu.seed % 7) * 0.4, PULSE_LEN + 2.0)
		var node: MeshInstance3D = pu.node
		if t > PULSE_LEN:
			node.visible = false
			continue
		node.visible = true
		var u := t / PULSE_LEN
		var r := lerpf(0.3, 1.5, 1.0 - pow(1.0 - u, 2.0))
		node.scale = Vector3(r, 1.0, r)
		(pu.mat as StandardMaterial3D).albedo_color = Color(1.0, 1.0, 1.0, 0.75 * (1.0 - u))

## Lines widen with the camera's distance, like the trails, so they are still there at region zoom.
func set_boost(value: float) -> void:
	boost = value
	for m in materials:
		(m as ShaderMaterial).set_shader_parameter("width_boost", value)

## The gallery stage's colonies: one in each state with its tether to a far capital, a galleon on station off the blockaded one, and a founding. `at` is the stage's centre
## in world space.
func load_demo(at: Vector3) -> void:
	demo = true
	var o := Vector2(at.x, at.z)
	var cap := o + Vector2(0.0, -48.0)
	var colonies := [
		{"pos": o + Vector2(-3.8, -42.0), "capital": cap, "civ": 0, "state": STATE_CONTENT},
		{"pos": o + Vector2(-1.3, -42.6), "capital": cap, "civ": 1, "state": STATE_STRAINED},
		{"pos": o + Vector2(1.3, -42.0), "capital": cap, "civ": 2, "state": STATE_BLOCKADED, "ship": o + Vector2(2.5, -41.0)},
		{"pos": o + Vector2(3.8, -42.6), "capital": cap, "civ": 3, "state": STATE_INDEPENDENT},
	]
	var foundings := [{"pos": o + Vector2(-0.4, -45.2), "yaw": 0.0, "civ": 0}]
	sync(colonies, foundings, at.y + 0.02)
