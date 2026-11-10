## Readable, directional links for internal sharing and external settlement supply.
class_name M7SupplyBuilder
extends RefCounted

const OUTLINE_COLOR := Color(0.035, 0.025, 0.02, 0.94)
const LINE_HALF_WIDTH := 0.067
const OUTLINE_HALF_WIDTH := 0.125
const LINE_LIFT := 0.31
const ARROW_LENGTH := 0.46
const ARROW_HALF_WIDTH := 0.23

static func make_material() -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	material.depth_draw_mode = BaseMaterial3D.DEPTH_DRAW_DISABLED
	material.no_depth_test = true
	material.vertex_color_use_as_albedo = true
	material.albedo_color = Color.WHITE
	material.render_priority = 12
	return material

static func build(w: M7WorldData) -> Dictionary:
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var colors := PackedColorArray()
	var labels: Array[Label3D] = []
	for i in w.supply_flows.size():
		var flow: Dictionary = w.supply_flows[i]
		var from_tile := int(flow.get("from", -1))
		var to_tile := int(flow.get("to", -1))
		if from_tile < 0 or from_tile >= w.width * w.height or to_tile < 0 or to_tile >= w.width * w.height or from_tile == to_tile:
			continue
		if _add_link(w, from_tile, to_tile, _link_colour(flow), i, vertices, normals, colors):
			labels.append(_flow_label(w, from_tile, to_tile, flow, i))
	for settlement in w.settlements:
		if bool(settlement.get("remembered", false)):
			continue
		var severity := int(settlement.get("supplyShortage", 0))
		if severity > 0:
			_add_shortage_ring(w, int(settlement.get("tile", -1)), severity, vertices, normals, colors)

	var mesh := ArrayMesh.new()
	if not vertices.is_empty():
		var arrays := []
		arrays.resize(Mesh.ARRAY_MAX)
		arrays[Mesh.ARRAY_VERTEX] = vertices
		arrays[Mesh.ARRAY_NORMAL] = normals
		arrays[Mesh.ARRAY_COLOR] = colors
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return {"mesh": mesh, "labels": labels}

static func _link_colour(flow: Dictionary) -> Color:
	var kind := int(flow.get("kind", 0))
	if kind == 1:
		return Color("#00dce8") # standing compact route or a caravan
	if kind == 2:
		return Color("#c16bff") # diplomatic gift or barter
	if kind == 3:
		return Color("#ff9b22") # tribute
	var good := String(flow.get("good", "")).to_upper()
	if good.contains("FOOD") or good.contains("GRAIN") or good.contains("MEAT") or good.contains("FISH"):
		return Color("#00d982") # food shared within a realm
	return Color("#368dff") # internal materials and tools

static func _add_link(w: M7WorldData, from_tile: int, to_tile: int, colour: Color, lane_index: int,
		vertices: PackedVector3Array, normals: PackedVector3Array, colors: PackedColorArray) -> bool:
	var from := w.tile_center(from_tile)
	var to := w.tile_center(to_tile)
	var delta := to - from
	if delta.length() < 0.85:
		return false
	var direction := delta.normalized()
	var side := Vector2(-direction.y, direction.x)
	var lane := float((lane_index % 7) - 3) * 0.11
	from += side * lane + direction * 0.36
	to += side * lane - direction * 0.43
	if from.distance_to(to) < 0.35:
		return false

	_add_strip(w, from, to, OUTLINE_HALF_WIDTH, LINE_LIFT - 0.015, OUTLINE_COLOR, vertices, normals, colors)
	_add_strip(w, from, to, LINE_HALF_WIDTH, LINE_LIFT, colour, vertices, normals, colors)
	# Two large arrowheads make direction legible at a glance, including on long routes.
	for fraction in [0.40, 0.73]:
		var center := from.lerp(to, fraction)
		_add_arrow(w, center, direction, side, ARROW_LENGTH + 0.12, ARROW_HALF_WIDTH + 0.10,
			LINE_LIFT + 0.005, OUTLINE_COLOR, vertices, normals, colors)
		_add_arrow(w, center, direction, side, ARROW_LENGTH, ARROW_HALF_WIDTH,
			LINE_LIFT + 0.025, colour, vertices, normals, colors)
	return true

static func _add_strip(w: M7WorldData, from: Vector2, to: Vector2, half_width: float, lift: float, tint: Color,
		vertices: PackedVector3Array, normals: PackedVector3Array, colors: PackedColorArray) -> void:
	var side := (to - from).normalized().orthogonal() * half_width
	var a := _world_point(w, from + side, lift)
	var b := _world_point(w, from - side, lift)
	var c := _world_point(w, to - side, lift)
	var d := _world_point(w, to + side, lift)
	_add_triangle(a, b, c, tint, vertices, normals, colors)
	_add_triangle(a, c, d, tint, vertices, normals, colors)

static func _add_arrow(w: M7WorldData, center: Vector2, direction: Vector2, side: Vector2, length: float,
		half_width: float, lift: float, tint: Color, vertices: PackedVector3Array,
		normals: PackedVector3Array, colors: PackedColorArray) -> void:
	var tip_2d := center + direction * length * 0.56
	var base := center - direction * length * 0.44
	_add_triangle(_world_point(w, tip_2d, lift),
		_world_point(w, base + side * half_width, lift),
		_world_point(w, base - side * half_width, lift), tint, vertices, normals, colors)

static func _flow_label(w: M7WorldData, from_tile: int, to_tile: int, flow: Dictionary, lane_index: int) -> Label3D:
	var from := w.tile_center(from_tile)
	var to := w.tile_center(to_tile)
	var direction := (to - from).normalized()
	var side := direction.orthogonal()
	var lane := float((lane_index % 7) - 3) * 0.11
	var center := from.lerp(to, 0.53) + side * lane
	var cargo := String(flow.get("label", flow.get("good", "Goods")))
	var amount := float(flow.get("amount", 0.0))
	if amount > 0.0:
		cargo += " · " + str(snappedf(amount, 0.1))
	var label := Label3D.new()
	label.text = cargo
	label.font_size = 32
	label.outline_size = 12
	label.pixel_size = 0.00055
	label.modulate = Color("#fff4dd")
	label.outline_modulate = Color(0.035, 0.025, 0.02, 1.0)
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.fixed_size = true
	label.no_depth_test = true
	label.shaded = false
	label.double_sided = true
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.render_priority = 13
	label.position = Vector3(center.x, w.height_at(center) + 0.82, center.y)
	return label

static func _add_shortage_ring(w: M7WorldData, tile: int, severity: int, vertices: PackedVector3Array,
		normals: PackedVector3Array, colors: PackedColorArray) -> void:
	if tile < 0 or tile >= w.width * w.height:
		return
	var center := w.tile_center(tile)
	var tint := Color("#ff4c43") if severity >= 3 else Color("#ffb226")
	var inner := 0.43
	var outer := 0.50
	var segments := 18
	for i in segments:
		var a0 := TAU * float(i) / segments
		var a1 := TAU * float(i + 1) / segments
		var p0 := center + Vector2(cos(a0), sin(a0)) * inner
		var p1 := center + Vector2(cos(a0), sin(a0)) * outer
		var p2 := center + Vector2(cos(a1), sin(a1)) * outer
		var p3 := center + Vector2(cos(a1), sin(a1)) * inner
		_add_triangle(_world_point(w, p0, LINE_LIFT + 0.015), _world_point(w, p1, LINE_LIFT + 0.015),
			_world_point(w, p2, LINE_LIFT + 0.015), tint, vertices, normals, colors)
		_add_triangle(_world_point(w, p0, LINE_LIFT + 0.015), _world_point(w, p2, LINE_LIFT + 0.015),
			_world_point(w, p3, LINE_LIFT + 0.015), tint, vertices, normals, colors)

static func _world_point(w: M7WorldData, p: Vector2, lift: float) -> Vector3:
	return Vector3(p.x, w.height_at(p) + lift, p.y)

static func _add_triangle(a: Vector3, b: Vector3, c: Vector3, tint: Color, vertices: PackedVector3Array,
		normals: PackedVector3Array, colors: PackedColorArray) -> void:
	vertices.append(a)
	vertices.append(b)
	vertices.append(c)
	for _i in 3:
		normals.append(Vector3.UP)
		colors.append(tint)
