## Builds the hex-prism terrain (chunked ArrayMeshes), territory borders and river ribbons.
class_name M7TerrainBuilder
extends RefCounted

const CHUNK := 20
const INNER := 0.72          # inner hex (flat top) as a fraction of the circumradius
const BEVEL := 0.035

# Summer base palette (sRGB), chosen as one harmonious, slightly warm family.
const BIOME_COL := [
	"#2c5a6b", # Ocean floor
	"#9fa07a", # Coast (shallow sand under water)
	"#e6eef0", # Ice
	"#9ea67c", # Tundra
	"#5b7447", # Taiga
	"#5f8137", # Temperate forest floor
	"#7d9842", # Open woodland
	"#8ba346", # Grassland / steppe
	"#a6994c", # Savanna
	"#557f36", # Tropical forest floor
	"#6c8a58", # Wetland
	"#dcc289", # Desert
	"#7d7466", # Mountain rock
	"#8f877c", # Peak rock
]
# How much the seasonal grade affects the ground (1 lush vegetation .. 0 bare rock/sand/ice).
const SAND := Color(0.80, 0.72, 0.52, 0.0)
const BIOME_VEG := [0.0, 0.0, 0.0, 0.6, 0.8, 1.0, 1.0, 1.0, 0.75, 0.9, 0.9, 0.1, 0.15, 0.0]

var w: M7WorldData
var tile_col := PackedColorArray()

func _init(world: M7WorldData) -> void:
	w = world
	var n := w.width * w.height
	tile_col.resize(n)
	for t in n:
		var b := int(w.biome[t])
		var base := Color(BIOME_COL[b])
		var xy := w.tile_xy(t)
		var k := M7WorldData.hash01(xy.x, xy.y, 7)
		var k2 := M7WorldData.hash01(xy.x, xy.y, 11)
		# subtle per-tile variation: value and a touch of hue
		var col := base.lightened(0.05 * k) if k > 0.5 else base.darkened(0.06 * (1.0 - k))
		col = col.lerp(Color(base.r * 1.05, base.g, base.b * 0.92), k2 * 0.5)
		if w.flags[t] & M7WorldData.FLAG_WILDGRAIN and b >= 6 and b <= 8:
			col = col.lerp(Color("#cdbb63"), 0.25)
		col.a = BIOME_VEG[b]   # alpha carries the seasonal 'vegetation' weight to the shader
		tile_col[t] = col

func cliff_color(t: int) -> Color:
	var b := int(w.biome[t])
	if b <= M7WorldData.Biome.COAST:
		return Color("#b9a77a")
	if b >= M7WorldData.Biome.MOUNTAIN:
		return Color("#6f675e")
	if b == M7WorldData.Biome.DESERT:
		return Color("#b8976a")
	if b == M7WorldData.Biome.ICE:
		return Color("#b8c8cf")
	return Color("#7a5f45")

## How far the outer rim of a tile edge drops toward a lower neighbour: single terrace steps
## between land tiles become a soft ramp; bigger drops get a small bevel and a cliff wall.
static func bevel_for(h: float, nh: float) -> float:
	var d := h - nh
	if d <= 0.001:
		return 0.0
	if d <= 0.105 and nh > 0.05:
		return d
	return minf(BEVEL, d * 0.5)

## Builds terrain chunk meshes and adds them under parent.
func build_terrain(parent: Node3D, material: Material) -> void:
	var cw := int(ceil(float(w.width) / CHUNK))
	var ch := int(ceil(float(w.height) / CHUNK))
	for cy in ch:
		for cx in cw:
			var mesh := _build_chunk(cx * CHUNK, cy * CHUNK, mini(w.width, (cx + 1) * CHUNK), mini(w.height, (cy + 1) * CHUNK))
			var mi := MeshInstance3D.new()
			mi.mesh = mesh
			mi.material_override = material
			mi.name = "TerrainChunk_%d_%d" % [cx, cy]
			parent.add_child(mi)

func _build_chunk(x0: int, y0: int, x1: int, y1: int) -> ArrayMesh:
	var V := PackedVector3Array()
	var N := PackedVector3Array()
	var C := PackedColorArray()
	var UV := PackedVector2Array()
	var UV2 := PackedVector2Array()
	var corners := []
	for i in 6:
		corners.append(M7WorldData.corner(i))
	for y in range(y0, y1):
		for x in range(x0, x1):
			var t := y * w.width + x
			var h: float = w.heights[t]
			var cen := w.tile_center(t)
			var col := tile_col[t]
			var b := int(w.biome[t])
			var veg: float = BIOME_VEG[b]
			var own := w.owner[t]
			var clm := float(w.claim[t]) / 255.0
			var uv := Vector2(veg, M7WorldData.hash01(x, y, 3))
			# UV2 stays flat across the tile and selects its live viewer-filtered state texel.
			var uv2 := Vector2((x + 0.5) / float(w.width), (y + 0.5) / float(w.height))
			# neighbour info
			var nb := []
			var nh := []
			for i in 6:
				var k := w.neighbour(t, i)
				nb.append(k)
				nh.append(w.heights[k] if k >= 0 else -0.8)
			# colour at each corner, blended with same-height neighbours
			var ccol := []
			var beach := []
			for i in 6:
				var a: int = nb[(i + 5) % 6]
				var bb: int = nb[i]
				var sum := col
				var cnt := 1.0
				if a >= 0 and absf(float(nh[(i + 5) % 6]) - h) < 0.001:
					sum += tile_col[a]
					cnt += 1.0
				if bb >= 0 and absf(float(nh[i]) - h) < 0.001:
					sum += tile_col[bb]
					cnt += 1.0
				var cc := Color(sum.r / cnt, sum.g / cnt, sum.b / cnt, sum.a / cnt)
				# beaches: corners that touch the sea turn to sand
				var is_beach := b > M7WorldData.Biome.COAST and b < M7WorldData.Biome.MOUNTAIN and ((a >= 0 and w.is_water(a)) or (bb >= 0 and w.is_water(bb)))
				if is_beach:
					cc = SAND
				beach.append(is_beach)
				ccol.append(cc)
			var c3 := Vector3(cen.x, h, cen.y)
			var inner := []
			var outer := []
			var drop := []
			for i in 6:
				var cp: Vector2 = corners[i]
				inner.append(Vector3(cen.x + cp.x * INNER, h, cen.y + cp.y * INNER))
				outer.append(Vector2(cen.x + cp.x, cen.y + cp.y))
				drop.append(float(nh[i]) < h - 0.001)
			# centre fan
			for i in 6:
				var k := (i + 1) % 6
				var ci: Color = col.lerp(ccol[i], 0.85 if beach[i] else 0.55)
				var ck: Color = col.lerp(ccol[k], 0.85 if beach[k] else 0.55)
				_tri(V, N, C, UV, UV2, c3, inner[i], inner[k], col, ci, ck, uv, uv2)
			# edges: flat or bevelled, plus cliff walls
			for i in 6:
				var k := (i + 1) % 6
				var bev := bevel_for(h, float(nh[i]))
				var o0 := Vector3(outer[i].x, h - bev, outer[i].y)
				var o1 := Vector3(outer[k].x, h - bev, outer[k].y)
				var ci: Color = col.lerp(ccol[i], 0.85 if beach[i] else 0.55)
				var ck: Color = col.lerp(ccol[k], 0.85 if beach[k] else 0.55)
				var oc0: Color = ccol[i]
				var oc1: Color = ccol[k]
				if drop[i]:
					oc0 = ci.darkened(0.1)
					oc1 = ck.darkened(0.1)
				_tri(V, N, C, UV, UV2, inner[i], o0, o1, ci, oc0, oc1, uv, uv2)
				_tri(V, N, C, UV, UV2, inner[i], o1, inner[k], ci, oc1, ck, uv, uv2)
				# corner wedge between edge i-1 and edge i at corner i
				var p := (i + 5) % 6
				var bevp := bevel_for(h, float(nh[p]))
				if absf(bevp - bev) > 0.0005:
					var oa := Vector3(outer[i].x, h - bevp, outer[i].y)
					_tri(V, N, C, UV, UV2, inner[i], oa, o0, ci, oc0, oc0, uv, uv2)
				if drop[i] and bev < h - float(nh[i]) - 0.0005:
					var bottom := float(nh[i]) - 0.06
					var cc := cliff_color(t)
					if nb[i] >= 0 and w.is_water(nb[i]) and b < M7WorldData.Biome.MOUNTAIN:
						cc = SAND.darkened(0.12)
					var top_c := cc.lerp(col, 0.25)
					var bot_c := cc.darkened(0.35)
					var b0 := Vector3(outer[i].x, bottom, outer[i].y)
					var b1 := Vector3(outer[k].x, bottom, outer[k].y)
					var wuv := Vector2(-1.0, uv.y)  # x < 0 marks walls for the shader
					_tri(V, N, C, UV, UV2, o0, b0, b1, top_c, bot_c, bot_c, wuv, uv2)
					_tri(V, N, C, UV, UV2, o0, b1, o1, top_c, bot_c, top_c, wuv, uv2)
	var mesh := ArrayMesh.new()
	if V.size() == 0:
		return mesh
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = V
	arr[Mesh.ARRAY_NORMAL] = N
	arr[Mesh.ARRAY_COLOR] = C
	arr[Mesh.ARRAY_TEX_UV] = UV
	arr[Mesh.ARRAY_TEX_UV2] = UV2
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	return mesh

## Triangle with outward-up orientation (terrain faces always point up or outward from the tile).
static func _tri(V: PackedVector3Array, N: PackedVector3Array, C: PackedColorArray, UV: PackedVector2Array,
		UV2: PackedVector2Array, a: Vector3, b: Vector3, c: Vector3, ca: Color, cb: Color, cc: Color,
		uv: Vector2, uv2: Vector2) -> void:
	var nn := (c - a).cross(b - a)
	if nn.length_squared() < 1e-14:
		return
	nn = nn.normalized()
	if nn.y < -0.001 or (absf(nn.y) <= 0.001 and false):
		var tb := b
		b = c
		c = tb
		var tc := cb
		cb = cc
		cc = tc
		nn = -nn
	V.append(a); V.append(b); V.append(c)
	N.append(nn); N.append(nn); N.append(nn)
	C.append(ca.srgb_to_linear()); C.append(cb.srgb_to_linear()); C.append(cc.srgb_to_linear())
	UV.append(uv); UV.append(uv); UV.append(uv)
	UV2.append(uv2); UV2.append(uv2); UV2.append(uv2)

# ------------------------------------------------------------------ walls need outward orientation
# Walls are vertical (n.y == 0); they are emitted in an order that faces outward:
# (o0, b0, b1) with o0->o1 running clockwise seen from above gives an outward normal.

# ------------------------------------------------------------------ borders

## Ribbons along every hex edge where ownership changes. Returns a mesh (vertex colour = civ colour,
## UV.x = position along the edge 0..1, UV.y = 0 at the edge .. 1 inward, UV2.x = strip kind, UV2.y = civ).
func build_borders(width_scale: float) -> ArrayMesh:
	var V := PackedVector3Array()
	var N := PackedVector3Array()
	var C := PackedColorArray()
	var UV := PackedVector2Array()
	var UV2 := PackedVector2Array()
	var corners := []
	for i in 6:
		corners.append(M7WorldData.corner(i))
	for t in w.width * w.height:
		var own := w.owner[t]
		if own < 0 or w.biome[t] == M7WorldData.Biome.OCEAN:
			continue
		var cen := w.tile_center(t)
		var h: float = maxf(w.heights[t], 0.0)
		var col := M7WorldData.civ_color(own).srgb_to_linear()
		for i in 6:
			var nb := w.neighbour(t, i)
			if nb >= 0 and w.owner[nb] == own:
				continue
			var k := (i + 1) % 6
			var nh: float = w.heights[nb] if nb >= 0 else -1.0
			var bev := 0.0
			if w.heights[t] > 0.0:
				bev = bevel_for(h, nh)
			var cp0: Vector2 = corners[i]
			var cp1: Vector2 = corners[k]
			# strips: [kind, inset_from, inset_to]  (fraction of circumradius, 0 = edge)
			var strips := [[0.0, 0.0, 0.035], [1.0, 0.035, 0.14], [0.0, 0.14, 0.17], [2.0, 0.17, 0.6]]
			for s in strips:
				if float(s[0]) == 2.0 and w.is_water(t):
					continue
				var f0: float = 1.0 - minf(float(s[1]) * width_scale, 0.95)
				var f1: float = 1.0 - minf(float(s[2]) * width_scale, 0.98)
				var yb := h + 0.012
				# points on the bevel ring follow the bevel down; the flat top is at h
				var ya := yb - bev * clampf((1.0 - INNER - s[1]) / (1.0 - INNER), 0.0, 1.0)
				var yc := yb - bev * clampf((1.0 - INNER - s[2]) / (1.0 - INNER), 0.0, 1.0)
				var a0 := Vector3(cen.x + cp0.x * f0, ya, cen.y + cp0.y * f0)
				var a1 := Vector3(cen.x + cp1.x * f0, ya, cen.y + cp1.y * f0)
				var b1 := Vector3(cen.x + cp1.x * f1, yc, cen.y + cp1.y * f1)
				var b0 := Vector3(cen.x + cp0.x * f1, yc, cen.y + cp0.y * f1)
				var kind: float = s[0]
				var u2 := Vector2(kind, float(own))
				_strip_quad(V, N, C, UV, UV2, a0, a1, b1, b0, col, u2)
	var mesh := ArrayMesh.new()
	if V.size() == 0:
		return mesh
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = V
	arr[Mesh.ARRAY_NORMAL] = N
	arr[Mesh.ARRAY_COLOR] = C
	arr[Mesh.ARRAY_TEX_UV] = UV
	arr[Mesh.ARRAY_TEX_UV2] = UV2
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	return mesh

static func _strip_quad(V, N, C, UV, UV2, a0: Vector3, a1: Vector3, b1: Vector3, b0: Vector3, col: Color, u2: Vector2) -> void:
	var pts := [a0, a1, b1, a0, b1, b0]
	var uvs := [Vector2(0, 0), Vector2(1, 0), Vector2(1, 1), Vector2(0, 0), Vector2(1, 1), Vector2(0, 1)]
	var nn := (b1 - a0).cross(a1 - a0).normalized()
	var flip := nn.y < 0.0
	if flip:
		nn = -nn
	var order := [0, 2, 1, 3, 5, 4] if flip else [0, 1, 2, 3, 4, 5]
	for j in order:
		V.append(pts[j])
		N.append(nn)
		C.append(col)
		UV.append(uvs[j])
		UV2.append(u2)

# ------------------------------------------------------------------ rivers

var river_paths: Array = []   # Array of PackedVector3Array (smoothed, downstream order)

func _river_tile(t: int) -> bool:
	return t >= 0 and (w.flags[t] & M7WorldData.FLAG_RIVER) != 0 and w.is_land(t) \
		and (t >= w.visibility.size() or w.visibility[t] > 0)

func _river_nbs(t: int) -> Array:
	var r := []
	for i in 6:
		var nb := w.neighbour(t, i)
		if _river_tile(nb):
			r.append(nb)
	return r

## Walks river tiles into chains between endpoints/junctions, adds the outlet into water,
## smooths them (Chaikin), adds a gentle meander and orders them downstream.
func compute_river_paths() -> void:
	river_paths.clear()
	var n := w.width * w.height
	var used := {}
	var starts := []
	for t in n:
		if _river_tile(t) and _river_nbs(t).size() != 2:
			starts.append(t)
	var chains := []
	for s in starts:
		for nb in _river_nbs(s):
			var key := "%d_%d" % [mini(s, nb), maxi(s, nb)]
			if used.has(key):
				continue
			var chain := [s]
			var prev: int = s
			var cur: int = nb
			used[key] = true
			while true:
				chain.append(cur)
				var nbs := _river_nbs(cur)
				if nbs.size() != 2:
					break
				var nx: int = nbs[0] if nbs[0] != prev else nbs[1]
				var k2 := "%d_%d" % [mini(cur, nx), maxi(cur, nx)]
				if used.has(k2):
					break
				used[k2] = true
				prev = cur
				cur = nx
			chains.append(chain)
	# isolated single river tiles
	for s in starts:
		if _river_nbs(s).is_empty():
			chains.append([s])
	for chain in chains:
		# orient downstream: lower end last
		if w.heights[chain[0]] < w.heights[chain[chain.size() - 1]]:
			chain.reverse()
		var pts := []
		for t in chain:
			pts.append(w.tile_center(t))
		# outlet: from the low end into adjacent water or a lower non-river tile
		var last: int = chain[chain.size() - 1]
		if _river_nbs(last).size() <= 1:
			var best := -1
			var bh := 1e9
			for i in 6:
				var nb := w.neighbour(last, i)
				if nb < 0 or _river_tile(nb):
					continue
				var hh: float = w.heights[nb] - (10.0 if w.is_water(nb) else 0.0)
				if hh < bh:
					bh = hh
					best = nb
			if best >= 0 and w.is_water(best):
				pts.append(w.tile_center(best))
			else:
				# the sim's river flags can skip a tile: bridge to the nearest lower river tile
				var lc := w.tile_center(last)
				var bt := -1
				var bd := 2.3
				var xy := w.tile_xy(last)
				for yy in range(xy.y - 3, xy.y + 4):
					for xx in range(xy.x - 3, xy.x + 4):
						if xx < 0 or yy < 0 or xx >= w.width or yy >= w.height:
							continue
						var t2 := yy * w.width + xx
						if not _river_tile(t2) or t2 in chain or w.heights[t2] > w.heights[last] + 0.001:
							continue
						var dd := w.tile_center(t2).distance_to(lc)
						if dd < bd:
							bd = dd
							bt = t2
				if bt >= 0:
					pts.append(w.tile_center(bt))
		if pts.size() < 2:
			continue
		# Chaikin smoothing, keeping the end points
		for it in 3:
			var q := [pts[0]]
			for j in pts.size() - 1:
				var a: Vector2 = pts[j]
				var b: Vector2 = pts[j + 1]
				q.append(a.lerp(b, 0.25))
				q.append(a.lerp(b, 0.75))
			q.append(pts[pts.size() - 1])
			pts = q
		# resample + meander
		var out := PackedVector3Array()
		var total := 0.0
		for j in pts.size() - 1:
			total += (pts[j] as Vector2).distance_to(pts[j + 1])
		var acc := 0.0
		var y_prev := 1e9
		var seed := float(chain[0] % 101)
		for j in pts.size():
			var p: Vector2 = pts[j]
			if j > 0:
				acc += (pts[j - 1] as Vector2).distance_to(p)
			var dir: Vector2
			if j < pts.size() - 1:
				dir = ((pts[j + 1] as Vector2) - p).normalized()
			else:
				dir = (p - (pts[j - 1] as Vector2)).normalized()
			var side := Vector2(-dir.y, dir.x)
			var env := clampf(minf(acc, total - acc) / 0.35, 0.0, 1.0)
			var m := (sin(acc * 4.1 + seed) * 0.6 + sin(acc * 9.3 + seed * 2.0) * 0.4) * 0.07 * env
			p += side * m
			var t := w.tile_at(p)
			var y: float = (w.heights[t] if w.is_land(t) else 0.0) + 0.016
			y = minf(y, y_prev)
			y_prev = y
			out.append(Vector3(p.x, y, p.y))
		river_paths.append(out)

## Ribbon mesh along the river paths: water (UV2.x = 0) over a darker bank strip (UV2.x = 1).
## UV.x runs across 0..1, UV.y downstream (world units * 3).
func build_rivers() -> ArrayMesh:
	if river_paths.is_empty():
		compute_river_paths()
	var V := PackedVector3Array()
	var N := PackedVector3Array()
	var UV := PackedVector2Array()
	var UV2 := PackedVector2Array()
	for pass_kind in [1.0, 0.0]:
		for path in river_paths:
			_river_pass(V, N, UV, UV2, path, pass_kind)
	var mesh := ArrayMesh.new()
	if V.size() == 0:
		return mesh
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = V
	arr[Mesh.ARRAY_NORMAL] = N
	arr[Mesh.ARRAY_TEX_UV] = UV
	arr[Mesh.ARRAY_TEX_UV2] = UV2
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	return mesh

func _river_pass(V, N, UV, UV2, path: PackedVector3Array, kind: float) -> void:
		var L := path.size()
		var acc := 0.0
		for j in L - 1:
			var a: Vector3 = path[j]
			var b: Vector3 = path[j + 1]
			var seg := a.distance_to(b)
			var f0 := float(j) / (L - 1)
			var f1 := float(j + 1) / (L - 1)
			var w0 := lerpf(0.07, 0.13, f0)
			var w1 := lerpf(0.07, 0.13, f1)
			var da := _dir_at(path, j)
			var db := _dir_at(path, j + 1)
			if kind > 0.5:
				_ribbon_seg(V, N, UV, UV2, a + Vector3(0, -0.004, 0), b + Vector3(0, -0.004, 0), da, db, w0 + 0.08, w1 + 0.08, acc, acc + seg, 1.0)
			else:
				_ribbon_seg(V, N, UV, UV2, a, b, da, db, w0, w1, acc, acc + seg, 0.0)
			acc += seg

static func _dir_at(path: PackedVector3Array, j: int) -> Vector2:
	var a: Vector3 = path[maxi(j - 1, 0)]
	var b: Vector3 = path[mini(j + 1, path.size() - 1)]
	var d := Vector2(b.x - a.x, b.z - a.z)
	return d.normalized() if d.length() > 1e-6 else Vector2(1, 0)

static func _ribbon_seg(V, N, UV, UV2, a: Vector3, b: Vector3, da: Vector2, db: Vector2, wa: float, wb: float,
		va: float, vb: float, kind: float) -> void:
	var sa := Vector3(-da.y, 0, da.x) * wa * 0.5
	var sb := Vector3(-db.y, 0, db.x) * wb * 0.5
	var p := [a - sa, a + sa, b + sb, b - sb]
	var uv := [Vector2(0, va * 3.0), Vector2(1, va * 3.0), Vector2(1, vb * 3.0), Vector2(0, vb * 3.0)]
	var nn: Vector3 = ((p[2] as Vector3) - (p[0] as Vector3)).cross((p[1] as Vector3) - (p[0] as Vector3))
	var idx := [0, 1, 2, 0, 2, 3]
	if nn.y < 0:
		idx = [0, 2, 1, 0, 3, 2]
	for j in idx:
		V.append(p[j])
		N.append(Vector3.UP)
		UV.append(uv[j])
		UV2.append(Vector2(kind, 0))
