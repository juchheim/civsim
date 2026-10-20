## Tiny flat-shaded mesh builder: every triangle gets its own vertices and face normal,
## vertex colour carries albedo (converted to linear) and a material tag in alpha:
##   1.0 plain   0.0 civ colour (INSTANCE_CUSTOM.rgb)   0.5 deciduous foliage   0.625 evergreen
##   0.375 crop   0.25 emissive   0.75 cloth (tinted per instance)   0.875 grass
## Triangles are oriented automatically so their normal points away from a hint point.
class_name M7MeshBuilder
extends RefCounted

const TAG_PLAIN := 1.0
const TAG_CIV := 0.0
const TAG_FOLIAGE := 0.5
const TAG_EVERGREEN := 0.625
const TAG_CROP := 0.375
const TAG_EMISSIVE := 0.25
const TAG_CLOTH := 0.75
const TAG_GRASS := 0.875

var v := PackedVector3Array()
var n := PackedVector3Array()
var c := PackedColorArray()
var xf := Transform3D.IDENTITY
var seed := 1
var tag := TAG_PLAIN

static func lin(col: Color, t: float = 1.0) -> Color:
	var l := col.srgb_to_linear()
	l.a = t
	return l

func rnd() -> float:
	seed = (seed * 1103515245 + 12345) & 0x7fffffff
	return float(seed & 0xffff) / 65536.0

func rnds() -> float:
	return rnd() * 2.0 - 1.0

## Adds a triangle (points in local space of xf); orients it away from `away` (local space).
func tri(a: Vector3, b: Vector3, cc: Vector3, col: Color, away = null) -> void:
	var A := xf * a
	var B := xf * b
	var C := xf * cc
	var nn := (C - A).cross(B - A)
	if nn.length_squared() < 1e-14:
		return
	nn = nn.normalized()
	if away != null:
		var h: Vector3 = xf * (away as Vector3)
		var cen := (A + B + C) / 3.0
		if nn.dot(cen - h) < 0.0:
			var t := B
			B = C
			C = t
			nn = -nn
	v.append(A); v.append(B); v.append(C)
	n.append(nn); n.append(nn); n.append(nn)
	var lc := lin(col, tag)
	c.append(lc); c.append(lc); c.append(lc)

func quad(a: Vector3, b: Vector3, cc: Vector3, d: Vector3, col: Color, away = null) -> void:
	tri(a, b, cc, col, away)
	tri(a, cc, d, col, away)

## Frustum / cylinder / cone along +Y from base centre p.
func prism(p: Vector3, r0: float, r1: float, h: float, sides: int, col_side: Color, col_top: Color,
		rot: float = 0.0, jitter: float = 0.0, cap_bottom: bool = false) -> void:
	var bot := []
	var top := []
	for i in sides:
		var a := rot + TAU * i / sides
		var j0 := 1.0 + jitter * rnds()
		var j1 := 1.0 + jitter * rnds()
		bot.append(p + Vector3(cos(a) * r0 * j0, 0.0, sin(a) * r0 * j0))
		top.append(p + Vector3(cos(a) * r1 * j1, h, sin(a) * r1 * j1))
	var axis := p + Vector3(0, h * 0.5, 0)
	for i in sides:
		var k := (i + 1) % sides
		if r1 <= 0.0001:
			tri(bot[i], bot[k], top[i], col_side, axis)
		else:
			quad(bot[i], bot[k], top[k], top[i], col_side, axis)
	if r1 > 0.0001:
		var tc := p + Vector3(0, h, 0)
		for i in sides:
			tri(tc, top[i], top[(i + 1) % sides], col_top, p)
	if cap_bottom:
		for i in sides:
			tri(p, bot[i], bot[(i + 1) % sides], col_side, p + Vector3(0, h, 0))

## Low-poly ellipsoid blob (lat-long), optionally jittered.
func blob(p: Vector3, sc: Vector3, seg: int, rings: int, col: Color, jitter: float = 0.0, col_bottom = null) -> void:
	var pts := []
	for r in range(rings + 1):
		var phi := PI * r / rings
		var row := []
		for s in seg:
			var th := TAU * (s + (0.5 if r % 2 == 1 else 0.0)) / seg
			var j := 1.0 + (jitter * rnds() if r > 0 and r < rings else 0.0)
			row.append(p + Vector3(sin(phi) * cos(th) * sc.x * j, cos(phi) * sc.y * j, sin(phi) * sin(th) * sc.z * j))
		pts.append(row)
	for r in rings:
		var cr: Color = col
		if col_bottom != null and r >= rings / 2:
			cr = col_bottom
		for s in seg:
			var s1 := (s + 1) % seg
			var a: Vector3 = pts[r][s]
			var b: Vector3 = pts[r][s1]
			var d: Vector3 = pts[r + 1][s]
			var e: Vector3 = pts[r + 1][s1]
			if r == 0:
				tri(a, e, d, cr, p)
			elif r == rings - 1:
				tri(a, b, d, cr, p)
			else:
				quad(a, b, e, d, cr, p)

## Axis-aligned box in local space of xf, centred at p.
func box(p: Vector3, size: Vector3, col: Color, col_top = null) -> void:
	var h := size * 0.5
	var cs := [
		p + Vector3(-h.x, -h.y, -h.z), p + Vector3(h.x, -h.y, -h.z), p + Vector3(h.x, -h.y, h.z), p + Vector3(-h.x, -h.y, h.z),
		p + Vector3(-h.x, h.y, -h.z), p + Vector3(h.x, h.y, -h.z), p + Vector3(h.x, h.y, h.z), p + Vector3(-h.x, h.y, h.z)]
	var ct: Color = col if col_top == null else col_top
	quad(cs[4], cs[5], cs[6], cs[7], ct, p)
	quad(cs[0], cs[1], cs[2], cs[3], col, p)
	quad(cs[0], cs[1], cs[5], cs[4], col, p)
	quad(cs[1], cs[2], cs[6], cs[5], col, p)
	quad(cs[2], cs[3], cs[7], cs[6], col, p)
	quad(cs[3], cs[0], cs[4], cs[7], col, p)

## A thin pole (square-ish prism) between two points.
func pole(a: Vector3, b: Vector3, r: float, col: Color, sides: int = 4) -> void:
	var dir := b - a
	var len := dir.length()
	if len < 1e-6:
		return
	var up := dir / len
	var side := up.cross(Vector3.FORWARD if absf(up.y) > 0.9 else Vector3.UP).normalized()
	var fwd := up.cross(side).normalized()
	var save := xf
	xf = xf * Transform3D(Basis(side, up, fwd), a)
	prism(Vector3.ZERO, r, r * 0.8, len, sides, col, col, PI / sides)
	xf = save

func commit(mesh: ArrayMesh = null) -> ArrayMesh:
	if mesh == null:
		mesh = ArrayMesh.new()
	if v.size() == 0:
		return mesh
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = v
	arr[Mesh.ARRAY_NORMAL] = n
	arr[Mesh.ARRAY_COLOR] = c
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	return mesh

func clear() -> void:
	v = PackedVector3Array()
	n = PackedVector3Array()
	c = PackedColorArray()
	xf = Transform3D.IDENTITY
	tag = TAG_PLAIN
