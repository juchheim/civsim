## Builds draped ribbon meshes (rivers, borders) for shaders/line.gdshader.
extends RefCounted

const TerrainBake = preload("res://scripts/terrain_bake.gd")


static func ribbon_mesh(polys: Array, bake: TerrainBake, lift: float, closed_loops: bool) -> ArrayMesh:
	# polys: [{pts: PackedVector2Array, widths: PackedFloat32Array or null, color: Color}]
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.set_custom_format(0, SurfaceTool.CUSTOM_RGBA_FLOAT)
	var any := false
	for poly in polys:
		var pts: PackedVector2Array = poly["pts"]
		var n := pts.size()
		if n < 2:
			continue
		var widths = poly.get("widths", null)
		var col: Color = poly.get("color", Color.WHITE)
		var count := n + 1 if closed_loops else n
		var arc := 0.0
		var prev_l := Vector3.ZERO
		var prev_r := Vector3.ZERO
		var prev_arc := 0.0
		for j in count:
			var i := j % n
			if j > 0:
				arc += pts[i].distance_to(pts[(j - 1) % n])
			var a: Vector2
			var b: Vector2
			if closed_loops:
				a = pts[(i - 1 + n) % n]
				b = pts[(i + 1) % n]
			else:
				a = pts[maxi(i - 1, 0)]
				b = pts[mini(i + 1, n - 1)]
			var d := (b - a).normalized()
			var perp := Vector2(-d.y, d.x)
			var hw: float = (widths[i] if widths != null else 0.03)
			var y := maxf(bake.height_at(pts[i]), 0.0) + lift
			# lift to the highest ground under the ribbon's full width so it never cuts into banks
			y = maxf(y, bake.height_at(pts[i] + perp * hw * 2.0) + lift * 0.5)
			y = maxf(y, bake.height_at(pts[i] - perp * hw * 2.0) + lift * 0.5)
			var c := Vector3(pts[i].x, y, pts[i].y)
			if j > 0:
				_quad(st, prev_l, prev_r, c, c, prev_arc, arc, poly, col, perp, hw)
				any = true
			prev_l = c
			prev_r = c
			prev_arc = arc
			poly["_perp_prev"] = perp
			poly["_hw_prev"] = hw
	if not any:
		return null
	return st.commit()


static func _quad(st: SurfaceTool, a: Vector3, _a2: Vector3, b: Vector3, _b2: Vector3, arc_a: float, arc_b: float,
		poly: Dictionary, col: Color, perp_b: Vector2, hw_b: float) -> void:
	var perp_a: Vector2 = poly.get("_perp_prev", perp_b)
	var hw_a: float = poly.get("_hw_prev", hw_b)
	var verts := [
		[a, -1.0, arc_a, perp_a, hw_a], [a, 1.0, arc_a, perp_a, hw_a], [b, 1.0, arc_b, perp_b, hw_b],
		[a, -1.0, arc_a, perp_a, hw_a], [b, 1.0, arc_b, perp_b, hw_b], [b, -1.0, arc_b, perp_b, hw_b]]
	for v in verts:
		st.set_color(col)
		st.set_uv(Vector2(v[2], v[1]))
		st.set_uv2(v[3])
		st.set_custom(0, Color(v[4], 0, 0, 0))
		st.add_vertex(v[0])
