## Coloured directional links for internal sharing and external settlement supply.
class_name M7SupplyBuilder
extends RefCounted

const LINE_HALF_WIDTH := 0.045
const LINE_LIFT := 0.16
const ARROW_LENGTH := 0.30
const ARROW_HALF_WIDTH := 0.19

static func make_material() -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	material.depth_draw_mode = BaseMaterial3D.DEPTH_DRAW_DISABLED
	material.vertex_color_use_as_albedo = true
	material.albedo_color = Color.WHITE
	material.render_priority = 2
	return material

static func build(w: M7WorldData) -> ArrayMesh:
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var colors := PackedColorArray()
	for i in w.supply_flows.size():
		var flow: Dictionary = w.supply_flows[i]
		var from_tile := int(flow.get("from", -1))
		var to_tile := int(flow.get("to", -1))
		if from_tile < 0 or from_tile >= w.width * w.height or to_tile < 0 or to_tile >= w.width * w.height or from_tile == to_tile:
			continue
		_add_link(w, from_tile, to_tile, _link_colour(flow), i, vertices, normals, colors)
	for settlement in w.settlements:
		if bool(settlement.get("remembered", false)):
			continue
		var severity := int(settlement.get("supplyShortage", 0))
		if severity > 0:
			_add_shortage_ring(w, int(settlement.get("tile", -1)), severity, vertices, normals, colors)

	var mesh := ArrayMesh.new()
	if vertices.is_empty():
		return mesh
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_COLOR] = colors
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh

static func _link_colour(flow: Dictionary) -> Color:
	var kind := int(flow.get("kind", 0))
	if kind == 1:
		return Color("#47d1d1") # standing compact route or a caravan
	if kind == 2:
		return Color("#c28cff") # diplomatic gift or barter
	if kind == 3:
		return Color("#f0aa42") # tribute
	var good := String(flow.get("good", "")).to_upper()
	if good.contains("FOOD") or good.contains("GRAIN") or good.contains("MEAT") or good.contains("FISH"):
		return Color("#6bd887") # food shared within a realm
	return Color("#67aef2") # internal materials and tools

static func _add_link(w: M7WorldData, from_tile: int, to_tile: int, colour: Color, lane_index: int,
		vertices: PackedVector3Array, normals: PackedVector3Array, colors: PackedColorArray) -> void:
	var from := w.tile_center(from_tile)
	var to := w.tile_center(to_tile)
	var delta := to - from
	if delta.length() < 0.85:
		return
	var direction := delta.normalized()
	var side := Vector2(-direction.y, direction.x)
	var lane := float((lane_index % 5) - 2) * 0.095
	from += side * lane + direction * 0.36
	to += side * lane - direction * 0.43
	if from.distance_to(to) < 0.35:
		return
	var start_left := _world_point(w, from + side * LINE_HALF_WIDTH, LINE_LIFT)
	var start_right := _world_point(w, from - side * LINE_HALF_WIDTH, LINE_LIFT)
	var end_left := _world_point(w, to + side * LINE_HALF_WIDTH, LINE_LIFT)
	var end_right := _world_point(w, to - side * LINE_HALF_WIDTH, LINE_LIFT)
	_add_triangle(start_left, start_right, end_right, colour, vertices, normals, colors)
	_add_triangle(start_left, end_right, end_left, colour, vertices, normals, colors)

	var arrow_center := from.lerp(to, 0.70) + direction * (ARROW_LENGTH * 0.18)
	var tip := _world_point(w, arrow_center + direction * (ARROW_LENGTH * 0.5), LINE_LIFT + 0.025)
	var base := arrow_center - direction * (ARROW_LENGTH * 0.5)
	var left := _world_point(w, base + side * ARROW_HALF_WIDTH, LINE_LIFT + 0.025)
	var right := _world_point(w, base - side * ARROW_HALF_WIDTH, LINE_LIFT + 0.025)
	_add_triangle(tip, left, right, colour, vertices, normals, colors)

static func _add_shortage_ring(w: M7WorldData, tile: int, severity: int, vertices: PackedVector3Array,
		normals: PackedVector3Array, colors: PackedColorArray) -> void:
	if tile < 0 or tile >= w.width * w.height:
		return
	var center := w.tile_center(tile)
	var tint := Color("#f45e54") if severity >= 3 else Color("#f0a847")
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
