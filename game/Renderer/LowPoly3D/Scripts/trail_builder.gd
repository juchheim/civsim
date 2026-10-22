## Worn trails: soft earth-coloured ribbons draped over the ground along the routes between villages (MatchFrame.Trail*).
## A run of explored tiles becomes a smoothed, slightly wandering ribbon that fades at its ends; wear (level 1 to 3) widens and
## darkens it. Draw order and blending keep it on top of the terrain without hiding rivers or buildings.
class_name M7TrailBuilder
extends RefCounted

const TRIM := 0.45                  # a trail stops short of the village it leaves
const STEP := 8                     # smoothing samples per tile
const FADE := 0.4                   # length over which a loose end fades out
const WIDTH := [0.0, 0.058, 0.08, 0.108]
const ALPHA := [0.0, 0.62, 0.72, 0.82]
const TINT := [Color.BLACK, Color("#94805a"), Color("#9c8156"), Color("#a78a5c")]
const LIFT := 0.012

static func make_material() -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = load("res://Renderer/LowPoly3D/Shaders/trail.gdshader")
	return m

static func _catmull(p0: Vector2, p1: Vector2, p2: Vector2, p3: Vector2, t: float) -> Vector2:
	var t2 := t * t
	var t3 := t2 * t
	return 0.5 * ((2.0 * p1) + (-p0 + p2) * t + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2 + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3)

## Smoothed points through the tile centres of one run.
static func _smooth(w: M7WorldData, run: PackedInt32Array) -> PackedVector2Array:
	var pts := PackedVector2Array()
	for t in run:
		pts.append(w.tile_center(t))
	var out := PackedVector2Array()
	var n := pts.size()
	for i in n - 1:
		var p0 := pts[maxi(i - 1, 0)]
		var p1 := pts[i]
		var p2 := pts[i + 1]
		var p3 := pts[mini(i + 2, n - 1)]
		for s in STEP:
			out.append(_catmull(p0, p1, p2, p3, float(s) / STEP))
	out.append(pts[n - 1])
	return out

static func _length(pts: PackedVector2Array) -> float:
	var total := 0.0
	for i in pts.size() - 1:
		total += pts[i].distance_to(pts[i + 1])
	return total

## Builds one mesh for every trail in the frame. `polys` receives each ribbon's centre line (so trees can be kept off the track).
static func build(w: M7WorldData, polys: Array = []) -> ArrayMesh:
	var V := PackedVector3Array()
	var C := PackedColorArray()
	var N := PackedVector3Array()
	var U := PackedVector2Array()
	var I := PackedInt32Array()
	for ti in range(0, w.trail_meta.size(), 5):
		var level := clampi(w.trail_meta[ti], 1, 3)
		var start := w.trail_meta[ti + 3]
		var length := w.trail_meta[ti + 4]
		var run := PackedInt32Array()
		var run_first := true
		for j in length:
			var tile := w.trail_tiles[start + j]
			if tile >= 0:
				run.append(tile)
			if (tile < 0 or j == length - 1) and run.size() >= 2:
				var begins_at_village := run_first and w.trail_tiles[start] >= 0
				var ends_at_village := j == length - 1 and tile >= 0
				_ribbon(w, run, level, begins_at_village, ends_at_village, ti * 31 + start, V, C, N, U, I, polys)
				run = PackedInt32Array()
			elif tile < 0:
				run = PackedInt32Array()
			if tile < 0:
				run_first = false
	var mesh := ArrayMesh.new()
	if V.size() == 0:
		return mesh
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = V
	arr[Mesh.ARRAY_COLOR] = C
	arr[Mesh.ARRAY_NORMAL] = N
	arr[Mesh.ARRAY_TEX_UV] = U       # each vertex's offset from the centre line: the shader widens the track with zoom
	arr[Mesh.ARRAY_INDEX] = I
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	return mesh

static func _ribbon(w: M7WorldData, run: PackedInt32Array, level: int, trim_start: bool, trim_end: bool, salt: int,
		V: PackedVector3Array, C: PackedColorArray, N: PackedVector3Array, U: PackedVector2Array, I: PackedInt32Array,
		polys: Array) -> void:
	var pts := _smooth(w, run)
	var total := _length(pts)
	var from_d := TRIM if trim_start else 0.0
	var to_d := total - (TRIM if trim_end else 0.0)
	if to_d - from_d < 0.15:
		return
	var width: float = WIDTH[level]
	var tint: Color = TINT[level]
	var peak: float = ALPHA[level]
	var base := V.size()
	var line := PackedVector2Array()
	var acc := 0.0
	var columns := 4
	var rows := 0
	for i in pts.size():
		if i > 0:
			acc += pts[i].distance_to(pts[i - 1])
		if acc < from_d or acc > to_d:
			continue
		var a := pts[maxi(i - 1, 0)]
		var b := pts[mini(i + 1, pts.size() - 1)]
		var dir := (b - a).normalized() if a != b else Vector2.RIGHT
		var nrm := Vector2(-dir.y, dir.x)
		# the track wanders a little and its width breathes
		var phase := acc * 5.0 + salt * 0.37
		var centre := pts[i] + nrm * (0.022 * sin(phase) + 0.012 * sin(phase * 2.3 + 1.7))
		var wd := width * (0.85 + 0.3 * (0.5 + 0.5 * sin(acc * 3.1 + salt)))
		# both ends fade out: at the village, and where the trail meets ground nobody has explored
		var fade := smoothstep(0.0, 1.0, minf(clampf((acc - from_d) / FADE, 0.0, 1.0), clampf((to_d - acc) / FADE, 0.0, 1.0)))
		var offs := [-0.5, -0.3, 0.3, 0.5]
		var alphas := [0.0, 1.0, 1.0, 0.0]
		var hs := 0.0
		var pos: Array = []
		for k in columns:
			var p: Vector2 = centre + nrm * (wd * float(offs[k]))
			pos.append(p)
			hs = maxf(hs, maxf(w.height_at(p), 0.0))
		for k in columns:
			var p2: Vector2 = pos[k]
			V.append(Vector3(p2.x, hs + LIFT, p2.y))
			N.append(Vector3.UP)
			U.append(p2 - centre)
			var col := tint.srgb_to_linear()
			col.a = peak * float(alphas[k]) * fade
			C.append(col)
		line.append(centre)
		rows += 1
	for r in rows - 1:
		for k in columns - 1:
			var i0 := base + r * columns + k
			var i1 := i0 + 1
			var i2 := i0 + columns
			var i3 := i2 + 1
			I.append_array([i0, i2, i1, i1, i2, i3])
	if line.size() > 1:
		polys.append(line)
