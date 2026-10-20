## Procedural low-poly props. Vertex COLOR.rgb is the base pigment (linear), COLOR.a = 1 marks the parts
## tinted per instance (canopy season colour, civ colour on figures and dwellings, species coat on animals).
## Smooth normals within each part (parts are kept apart so each blob shades as one soft form).
extends RefCounted

static var _cache := {}
static var _face := 0
static var _ctr := Vector3.ZERO # every triangle is turned to face away from this point (the part's centre)


static func get_mesh(key: String) -> ArrayMesh:
	if _cache.has(key):
		return _cache[key]
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	_face = 0
	match key:
		"broadleaf": _broadleaf(st, false)
		"broadleaf_lo": _broadleaf(st, true)
		"conifer": _conifer(st, false)
		"conifer_lo": _conifer(st, true)
		"tropical": _tropical(st, false)
		"tropical_lo": _tropical(st, true)
		"acacia": _acacia(st)
		"shrub": _shrub(st)
		"fruit": _fruit(st)
		"rock": _rock(st)
		"peak0": _peak(st, 0.0)
		"peak1": _peak(st, 1.0)
		"peak2": _peak(st, 2.0)
		"adult": _person(st, 1.0, 0.0, false)
		"adult_spear": _person(st, 1.0, 0.0, true)
		"child": _person(st, 0.62, 0.0, false)
		"elder": _person(st, 0.95, 0.35, false)
		"animal": _animal(st, false)
		"animal_horned": _animal(st, true)
		"tent": _tent(st)
		"lean_to": _lean_to(st)
		"pit_house": _pit_house(st)
		"wattle": _wattle(st)
		"mudbrick": _mudbrick(st)
		"longhouse": _longhouse(st)
		"storage_pit": _storage_pit(st)
		"drying_rack": _drying_rack(st)
		"granary": _granary(st)
		"kiln": _kiln(st)
		"quern": _quern(st)
		"thorn": _thorn(st)
		"lookout": _lookout(st)
		"hearth": _hearth(st)
		"fence": _fence(st)
		"stake": _stake(st)
		"pole": _pole(st)
	st.index()
	st.generate_normals()
	var m := st.commit()
	_cache[key] = m
	return m


static func _c(hex: String, mask := 0.0) -> Color:
	var c := Color(hex).srgb_to_linear()
	c.a = mask
	return c


static func _tri(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, col: Color, cb = null, cc = null) -> void:
	# Godot's front faces are clockwise seen from outside, i.e. the emitted normal points inward
	var colb: Color = col if cb == null else cb
	var colc: Color = col if cc == null else cc
	var n := (c - a).cross(b - a)
	var m := (a + b + c) / 3.0 - _ctr
	var verts := [[a, col], [c, colc], [b, colb]] if n.dot(m) < 0.0 else [[a, col], [b, colb], [c, colc]]
	for v in verts:
		st.set_color(v[1])
		st.set_uv(Vector2(_face, 0))
		st.add_vertex(v[0])


static func _hashf(x: float) -> float:
	var h := sin(x * 91.3458) * 47453.5453
	return h - floorf(h)


## Lumpy ellipsoid. `rough` adds smooth bumps so canopies read as clustered leaves.
static func blob(st: SurfaceTool, c: Vector3, r: Vector3, col: Color, seg := 9, rings := 6, rough := 0.12, seed := 0.0) -> void:
	_face += 1
	_ctr = c
	var pts := []
	for i in rings + 1:
		var row := []
		var th := PI * i / rings
		for j in seg:
			var ph := TAU * j / seg
			var d := Vector3(sin(th) * cos(ph), cos(th), sin(th) * sin(ph))
			var bump := 1.0
			if i > 0 and i < rings:
				bump += rough * (sin(d.x * 4.1 + seed) * sin(d.z * 3.7 + seed * 1.3) + 0.6 * sin(d.y * 5.3 + seed * 2.1))
			row.append(c + Vector3(d.x * r.x, d.y * r.y, d.z * r.z) * bump)
		pts.append(row)
	for i in rings:
		for j in seg:
			var j2 := (j + 1) % seg
			var a: Vector3 = pts[i][j]
			var b: Vector3 = pts[i][j2]
			var c2: Vector3 = pts[i + 1][j]
			var d2: Vector3 = pts[i + 1][j2]
			if i > 0:
				_tri(st, a, b, c2, col)
			if i < rings - 1:
				_tri(st, b, d2, c2, col)


static func cone(st: SurfaceTool, base: Vector3, r: float, h: float, col: Color, seg := 8, top_r := 0.0, col_top = null) -> void:
	_face += 1
	_ctr = base + Vector3(0, h * 0.35, 0)
	var ct: Color = col if col_top == null else col_top
	for j in seg:
		var a0 := TAU * j / seg
		var a1 := TAU * ((j + 1) % seg) / seg
		var p0 := base + Vector3(cos(a0) * r, 0, sin(a0) * r)
		var p1 := base + Vector3(cos(a1) * r, 0, sin(a1) * r)
		if top_r <= 0.0:
			var apex := base + Vector3(0, h, 0)
			_tri(st, p0, p1, apex, col, col, ct)
		else:
			var q0 := base + Vector3(cos(a0) * top_r, h, sin(a0) * top_r)
			var q1 := base + Vector3(cos(a1) * top_r, h, sin(a1) * top_r)
			_tri(st, p0, p1, q0, col)
			_tri(st, p1, q1, q0, col)
	if top_r > 0.0:
		_face += 1
		var top := base + Vector3(0, h, 0)
		for j in seg:
			var a0 := TAU * j / seg
			var a1 := TAU * ((j + 1) % seg) / seg
			_tri(st, top, top + Vector3(cos(a1) * top_r, 0, sin(a1) * top_r), top + Vector3(cos(a0) * top_r, 0, sin(a0) * top_r), ct)


static func cyl(st: SurfaceTool, a: Vector3, b: Vector3, r: float, col: Color, seg := 6, r2 := -1.0) -> void:
	_face += 1
	_ctr = (a + b) * 0.5
	var rb := r if r2 < 0 else r2
	var axis := (b - a).normalized()
	var t := axis.cross(Vector3.UP if absf(axis.y) < 0.9 else Vector3.RIGHT).normalized()
	var bt := axis.cross(t)
	for j in seg:
		var a0 := TAU * j / seg
		var a1 := TAU * ((j + 1) % seg) / seg
		var d0 := t * cos(a0) + bt * sin(a0)
		var d1 := t * cos(a1) + bt * sin(a1)
		_tri(st, a + d0 * r, a + d1 * r, b + d0 * rb, col)
		_tri(st, a + d1 * r, b + d1 * rb, b + d0 * rb, col)


static func box(st: SurfaceTool, c: Vector3, s: Vector3, col: Color, yaw := 0.0, top_col = null) -> void:
	var h := s * 0.5
	_ctr = c
	var basis := Basis(Vector3.UP, yaw)
	var corners := []
	for i in 8:
		var v := Vector3(h.x if (i & 1) else -h.x, h.y if (i & 2) else -h.y, h.z if (i & 4) else -h.z)
		corners.append(c + basis * v)
	var faces := [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]]
	for fi in faces.size():
		var f: Array = faces[fi]
		_face += 1
		var fc: Color = col
		if fi == 3 and top_col != null:
			fc = top_col
		_tri(st, corners[f[0]], corners[f[2]], corners[f[1]], fc)
		_tri(st, corners[f[0]], corners[f[3]], corners[f[2]], fc)


## Gable roof: ridge along local X.
static func gable(st: SurfaceTool, c: Vector3, length: float, width: float, h: float, col: Color, yaw := 0.0, overhang := 0.01) -> void:
	var basis := Basis(Vector3.UP, yaw)
	var L := length * 0.5 + overhang
	var W := width * 0.5 + overhang
	var p := func(x: float, y: float, z: float) -> Vector3: return c + basis * Vector3(x, y, z)
	_ctr = c + Vector3(0, h * 0.3, 0)
	var a: Vector3 = p.call(-L, 0, -W)
	var b: Vector3 = p.call(L, 0, -W)
	var r0: Vector3 = p.call(-L * 0.96, h, 0)
	var r1: Vector3 = p.call(L * 0.96, h, 0)
	var d: Vector3 = p.call(-L, 0, W)
	var e: Vector3 = p.call(L, 0, W)
	_face += 1
	_tri(st, a, r0, b, col)
	_tri(st, b, r0, r1, col)
	_face += 1
	_tri(st, d, e, r0, col)
	_tri(st, e, r1, r0, col)
	_face += 1
	_tri(st, a, d, r0, col.darkened(0.15))
	_face += 1
	_tri(st, b, r1, e, col.darkened(0.15))


# ---------------------------------------------------------------- trees

static func _broadleaf(st: SurfaceTool, lo: bool) -> void:
	var bark := _c("#5e4636")
	var leaf := Color(1, 1, 1, 1)
	cyl(st, Vector3(0, -0.02, 0), Vector3(0, 0.11, 0), 0.012, bark, 5, 0.008)
	if lo:
		blob(st, Vector3(0, 0.17, 0), Vector3(0.09, 0.08, 0.09), leaf, 7, 4, 0.1, 1.0)
		return
	blob(st, Vector3(0, 0.18, 0), Vector3(0.085, 0.075, 0.085), leaf, 10, 6, 0.14, 1.0)
	blob(st, Vector3(0.055, 0.14, 0.02), Vector3(0.06, 0.055, 0.06), Color(0.93, 0.95, 0.9, 1), 8, 5, 0.14, 2.0)
	blob(st, Vector3(-0.045, 0.15, -0.035), Vector3(0.062, 0.058, 0.062), Color(0.95, 0.93, 0.92, 1), 8, 5, 0.14, 3.0)


static func _conifer(st: SurfaceTool, lo: bool) -> void:
	var bark := _c("#5a4335")
	var leaf := Color(1, 1, 1, 1)
	cyl(st, Vector3(0, -0.02, 0), Vector3(0, 0.06, 0), 0.01, bark, 5)
	if lo:
		cone(st, Vector3(0, 0.03, 0), 0.06, 0.22, leaf, 6)
		return
	cone(st, Vector3(0, 0.035, 0), 0.068, 0.11, leaf, 9)
	cone(st, Vector3(0, 0.095, 0), 0.054, 0.1, Color(0.96, 0.97, 0.96, 1), 9)
	cone(st, Vector3(0, 0.15, 0), 0.038, 0.09, Color(0.92, 0.95, 0.93, 1), 8)


static func _tropical(st: SurfaceTool, lo: bool) -> void:
	var bark := _c("#6a5240")
	var leaf := Color(1, 1, 1, 1)
	cyl(st, Vector3(0, -0.02, 0), Vector3(0.01, 0.15, 0), 0.013, bark, 5, 0.009)
	if lo:
		blob(st, Vector3(0.01, 0.19, 0), Vector3(0.11, 0.06, 0.11), leaf, 7, 4, 0.1, 4.0)
		return
	blob(st, Vector3(0.01, 0.2, 0), Vector3(0.105, 0.06, 0.1), leaf, 10, 6, 0.16, 4.0)
	blob(st, Vector3(0.07, 0.17, 0.03), Vector3(0.06, 0.045, 0.06), Color(0.92, 0.95, 0.9, 1), 8, 5, 0.15, 5.0)
	blob(st, Vector3(-0.06, 0.175, -0.04), Vector3(0.065, 0.045, 0.06), Color(0.95, 0.92, 0.94, 1), 8, 5, 0.15, 6.0)
	blob(st, Vector3(0.0, 0.245, 0.0), Vector3(0.06, 0.04, 0.06), Color(1.04, 1.04, 1.0, 1), 8, 4, 0.15, 7.0)


static func _acacia(st: SurfaceTool) -> void:
	var bark := _c("#5e4636")
	var leaf := Color(1, 1, 1, 1)
	cyl(st, Vector3(0, -0.02, 0), Vector3(0.01, 0.09, 0), 0.009, bark, 5, 0.007)
	cyl(st, Vector3(0.01, 0.085, 0), Vector3(0.05, 0.14, 0.01), 0.006, bark, 4)
	cyl(st, Vector3(0.01, 0.085, 0), Vector3(-0.04, 0.135, -0.01), 0.006, bark, 4)
	blob(st, Vector3(0.0, 0.15, 0), Vector3(0.12, 0.028, 0.1), leaf, 11, 4, 0.12, 8.0)


static func _shrub(st: SurfaceTool) -> void:
	var leaf := Color(1, 1, 1, 1)
	blob(st, Vector3(0, 0.025, 0), Vector3(0.045, 0.035, 0.045), leaf, 8, 5, 0.18, 9.0)
	blob(st, Vector3(0.035, 0.018, 0.02), Vector3(0.03, 0.025, 0.03), Color(0.93, 0.95, 0.9, 1), 7, 4, 0.18, 10.0)


static func _fruit(st: SurfaceTool) -> void:
	var bark := _c("#5e4636")
	cyl(st, Vector3(0, -0.02, 0), Vector3(0, 0.06, 0), 0.008, bark, 5)
	blob(st, Vector3(0, 0.09, 0), Vector3(0.05, 0.045, 0.05), Color(1, 1, 1, 1), 9, 5, 0.12, 11.0)


static func _rock(st: SurfaceTool) -> void:
	blob(st, Vector3(0, 0.01, 0), Vector3(0.06, 0.04, 0.05), Color(1, 1, 1, 1), 6, 4, 0.35, 12.0)


## Faceted mountain: unit height, base radius ~1. Rings are jittered so each facet catches light differently.
static func _peak(st: SurfaceTool, seed: float) -> void:
	var rock := Color(1, 1, 1, 1)
	_ctr = Vector3(0, 0.3, 0)
	var n := 7
	var rings := [[0.0, 1.0], [0.3, 0.72], [0.58, 0.42], [0.82, 0.17]]
	var pts := []
	for ri in rings.size():
		var row := []
		for j in n:
			var a := TAU * (j + 0.5 * ri + 0.3 * _hashf(seed * 7.0 + j + ri * 13.0)) / n
			var r: float = rings[ri][1] * (0.78 + 0.4 * _hashf(seed * 3.0 + j * 1.7 + ri * 5.1))
			var lean: Vector3 = Vector3(0.12, 0, 0.06) * float(rings[ri][0]) * (seed - 1.0)
			row.append(Vector3(cos(a) * r, rings[ri][0] + 0.04 * _hashf(seed + j * 2.3 + ri), sin(a) * r) + lean)
		pts.append(row)
	var apex := Vector3(0.05 * (seed - 1.0), 1.0, 0.03)
	for ri in rings.size() - 1:
		for j in n:
			var j2 := (j + 1) % n
			_face += 1
			_tri(st, pts[ri][j], pts[ri + 1][j], pts[ri][j2], rock)
			_face += 1
			_tri(st, pts[ri][j2], pts[ri + 1][j], pts[ri + 1][j2], rock)
	var top: Array = pts[rings.size() - 1]
	for j in n:
		_face += 1
		_tri(st, top[j], apex, top[(j + 1) % n], rock)


# ---------------------------------------------------------------- people and animals

static func _person(st: SurfaceTool, s: float, stoop: float, spear: bool) -> void:
	var skin := _c("#b0805a")
	var hide := _c("#a88660")
	var dark := _c("#4a3628")
	var sash := Color(1, 1, 1, 1)
	var lean := Vector3(0, 0, stoop * 0.012) * s
	# legs
	cyl(st, Vector3(-0.007, 0, 0) * s, Vector3(-0.006, 0.028, 0) * s, 0.0055 * s, dark, 5)
	cyl(st, Vector3(0.007, 0, 0) * s, Vector3(0.006, 0.028, 0) * s, 0.0055 * s, dark, 5)
	# tunic (a tapered cone) and the civ sash band
	cyl(st, Vector3(0, 0.022, 0) * s, Vector3(0, 0.058, 0) * s + lean, 0.0135 * s, hide, 7, 0.0105 * s)
	cyl(st, Vector3(0, 0.034, 0) * s + lean * 0.5, Vector3(0, 0.05, 0) * s + lean * 0.8, 0.0146 * s, sash, 7, 0.0128 * s)
	# arms
	cyl(st, Vector3(-0.013, 0.055, 0) * s + lean, Vector3(-0.017, 0.032, 0.004) * s + lean, 0.0042 * s, hide, 4)
	cyl(st, Vector3(0.013, 0.055, 0) * s + lean, Vector3(0.017, 0.032, 0.004) * s + lean, 0.0042 * s, hide, 4)
	# big toy-like head with dark hair cap
	blob(st, Vector3(0, 0.071, 0) * s + lean * 1.3, Vector3(0.0115, 0.0125, 0.0115) * s, skin, 8, 5, 0.0, 0.0)
	blob(st, Vector3(0, 0.0765, -0.002) * s + lean * 1.3, Vector3(0.0118, 0.0075, 0.0118) * s, dark, 8, 4, 0.0, 0.0)
	if spear:
		cyl(st, Vector3(0.02, 0.0, 0.005) * s, Vector3(0.024, 0.11, 0.0) * s, 0.0022 * s, _c("#6b5138"), 4)
		cone(st, Vector3(0.024, 0.108, 0.0) * s, 0.004 * s, 0.014 * s, _c("#8d8a86"), 4)
	if stoop > 0.0:
		cyl(st, Vector3(0.02, 0.0, 0.01) * s, Vector3(0.018, 0.06, 0.01) * s, 0.002 * s, _c("#6b5138"), 4)


static func _animal(st: SurfaceTool, horned: bool) -> void:
	var coat := Color(1, 1, 1, 1)
	var dark := Color(0.55, 0.5, 0.48, 1)
	blob(st, Vector3(0, 0.032, 0), Vector3(0.03, 0.015, 0.013), coat, 8, 5, 0.04, 0.0)
	for lx in [-0.018, 0.018]:
		for lz in [-0.007, 0.007]:
			cyl(st, Vector3(lx, 0.0, lz), Vector3(lx, 0.026, lz), 0.0035, _c("#3e2e24"), 4)
	cyl(st, Vector3(0.026, 0.036, 0), Vector3(0.036, 0.05, 0), 0.006, coat, 5)
	blob(st, Vector3(0.041, 0.052, 0), Vector3(0.011, 0.0075, 0.0075), dark, 6, 4, 0.0, 0.0)
	if horned:
		cyl(st, Vector3(0.038, 0.058, 0.004), Vector3(0.034, 0.072, 0.012), 0.0018, _c("#d9ccb0"), 3)
		cyl(st, Vector3(0.038, 0.058, -0.004), Vector3(0.034, 0.072, -0.012), 0.0018, _c("#d9ccb0"), 3)


# ---------------------------------------------------------------- structures (door faces +X)

static func _tent(st: SurfaceTool) -> void:
	var hide := _c("#d8bc8c")
	var band := Color(1, 1, 1, 1)
	cone(st, Vector3(0, 0, 0), 0.05, 0.03, hide, 10, 0.034)
	cone(st, Vector3(0, 0.03, 0), 0.034, 0.012, band, 10, 0.029)
	cone(st, Vector3(0, 0.042, 0), 0.029, 0.04, hide, 10)
	var pole := _c("#5e4636")
	for i in 3:
		var a := TAU * i / 3.0 + 0.4
		cyl(st, Vector3(cos(a) * 0.005, 0.07, sin(a) * 0.005), Vector3(cos(a) * 0.016, 0.1, sin(a) * 0.016), 0.0022, pole, 3)
	box(st, Vector3(0.042, 0.012, 0), Vector3(0.006, 0.024, 0.018), _c("#3a2b22"))


static func _lean_to(st: SurfaceTool) -> void:
	var thatch := _c("#b59a62")
	var pole := _c("#5e4636")
	_face += 1
	var a := Vector3(-0.035, 0.0, -0.04)
	var b := Vector3(-0.035, 0.0, 0.04)
	var c := Vector3(0.03, 0.055, -0.04)
	var d := Vector3(0.03, 0.055, 0.04)
	_ctr = Vector3(0.03, 0.0, 0)
	_tri(st, a, c, b, thatch)
	_tri(st, b, c, d, thatch)
	_face += 1
	_ctr = Vector3(-0.06, 0.08, 0)
	_tri(st, a, b, c, thatch.darkened(0.25))
	_tri(st, b, d, c, thatch.darkened(0.25))
	cyl(st, Vector3(0.03, 0, -0.035), Vector3(0.03, 0.058, -0.035), 0.003, pole, 4)
	cyl(st, Vector3(0.03, 0, 0.035), Vector3(0.03, 0.058, 0.035), 0.003, pole, 4)


static func _pit_house(st: SurfaceTool) -> void:
	var turf := _c("#a89060")
	blob(st, Vector3(0, -0.005, 0), Vector3(0.06, 0.045, 0.06), turf, 12, 6, 0.06, 2.0)
	cone(st, Vector3(0, 0.03, 0), 0.012, 0.02, _c("#5e4636"), 6)
	box(st, Vector3(0.05, 0.012, 0), Vector3(0.03, 0.024, 0.022), turf.darkened(0.1))
	box(st, Vector3(0.066, 0.01, 0), Vector3(0.003, 0.016, 0.012), _c("#2e231c"))


static func _wattle(st: SurfaceTool) -> void:
	var wall := _c("#c7a46e")
	var thatch := _c("#c9a85a")
	cone(st, Vector3(0, 0, 0), 0.043, 0.036, wall, 12, 0.042)
	cone(st, Vector3(0, 0.032, 0), 0.058, 0.055, thatch, 12, 0.0, _c("#a98c4c"))
	box(st, Vector3(0.042, 0.014, 0), Vector3(0.004, 0.026, 0.016), _c("#3a2b22"))
	box(st, Vector3(0.0, 0.052, 0.0), Vector3(0.004, 0.012, 0.03), Color(1, 1, 1, 1))


static func _mudbrick(st: SurfaceTool) -> void:
	var clay := _c("#cf9f6a")
	box(st, Vector3(0, 0.024, 0), Vector3(0.07, 0.048, 0.058), clay, 0.0, _c("#d9b07e"))
	box(st, Vector3(0, 0.05, 0), Vector3(0.074, 0.006, 0.062), _c("#b98b5c"))
	box(st, Vector3(0.036, 0.016, 0.0), Vector3(0.003, 0.03, 0.016), _c("#3a2b22"))
	box(st, Vector3(-0.02, 0.058, 0.015), Vector3(0.014, 0.01, 0.014), Color(1, 1, 1, 1))


static func _longhouse(st: SurfaceTool) -> void:
	var wall := _c("#a07a54")
	var thatch := _c("#c3a35c")
	box(st, Vector3(0, 0.018, 0), Vector3(0.16, 0.036, 0.055), wall)
	gable(st, Vector3(0, 0.036, 0), 0.16, 0.055, 0.05, thatch, 0.0, 0.012)
	box(st, Vector3(0.081, 0.014, 0), Vector3(0.004, 0.026, 0.016), _c("#3a2b22"))
	box(st, Vector3(0.0, 0.03, 0.03), Vector3(0.05, 0.008, 0.003), Color(1, 1, 1, 1))


static func _storage_pit(st: SurfaceTool) -> void:
	blob(st, Vector3(0, -0.004, 0), Vector3(0.026, 0.014, 0.026), _c("#8d7a52"), 8, 4, 0.05, 1.0)
	cone(st, Vector3(0, 0.008, 0), 0.016, 0.004, _c("#5a4436"), 8, 0.014)


static func _drying_rack(st: SurfaceTool) -> void:
	var pole := _c("#5e4636")
	cyl(st, Vector3(-0.03, 0, 0), Vector3(-0.03, 0.05, 0), 0.0025, pole, 4)
	cyl(st, Vector3(0.03, 0, 0), Vector3(0.03, 0.05, 0), 0.0025, pole, 4)
	cyl(st, Vector3(-0.033, 0.048, 0), Vector3(0.033, 0.048, 0), 0.0022, pole, 4)
	for i in 4:
		var x := -0.021 + i * 0.014
		box(st, Vector3(x, 0.036, 0), Vector3(0.008, 0.022, 0.002), _c("#a5543c") if i % 2 == 0 else _c("#c08a5a"))


static func _granary(st: SurfaceTool) -> void:
	var pole := _c("#5e4636")
	for i in 4:
		var a := TAU * i / 4.0 + 0.78
		cyl(st, Vector3(cos(a) * 0.02, 0, sin(a) * 0.02), Vector3(cos(a) * 0.02, 0.03, sin(a) * 0.02), 0.003, pole, 4)
	cone(st, Vector3(0, 0.028, 0), 0.03, 0.03, _c("#c7a46e"), 10, 0.03)
	cone(st, Vector3(0, 0.056, 0), 0.04, 0.04, _c("#c9a85a"), 10)


static func _kiln(st: SurfaceTool) -> void:
	blob(st, Vector3(0, 0.0, 0), Vector3(0.034, 0.038, 0.034), _c("#b9774c"), 10, 6, 0.05, 2.0)
	cyl(st, Vector3(0, 0.03, 0), Vector3(0, 0.05, 0), 0.008, _c("#9c6440"), 6)
	box(st, Vector3(0.03, 0.01, 0), Vector3(0.006, 0.014, 0.014), _c("#2a1e17"))


static func _quern(st: SurfaceTool) -> void:
	cone(st, Vector3(0, 0, 0), 0.022, 0.01, _c("#8f8a84"), 10, 0.02)
	blob(st, Vector3(0.004, 0.014, 0), Vector3(0.008, 0.005, 0.006), _c("#a29c94"), 6, 4, 0.0, 0.0)


static func _thorn(st: SurfaceTool) -> void:
	var t := _c("#8a7650")
	for i in 7:
		var x := -0.045 + i * 0.015
		var lean := 0.01 * sin(i * 2.3)
		cone(st, Vector3(x, 0, 0), 0.012, 0.03 + 0.008 * sin(i * 1.7), t, 5)
		cyl(st, Vector3(x, 0.006, 0), Vector3(x + lean + 0.006, 0.028, 0.012), 0.0018, t, 3)
		cyl(st, Vector3(x, 0.006, 0), Vector3(x - lean, 0.026, -0.012), 0.0018, t, 3)


static func _lookout(st: SurfaceTool) -> void:
	var pole := _c("#5e4636")
	for i in 4:
		var a := TAU * i / 4.0 + 0.78
		cyl(st, Vector3(cos(a) * 0.022, 0, sin(a) * 0.022), Vector3(cos(a) * 0.012, 0.16, sin(a) * 0.012), 0.003, pole, 4)
	box(st, Vector3(0, 0.162, 0), Vector3(0.036, 0.006, 0.036), _c("#7d5d40"))
	cone(st, Vector3(0, 0.185, 0), 0.03, 0.03, _c("#c9a85a"), 8)
	for i in 4:
		var a := TAU * i / 4.0 + 0.78
		cyl(st, Vector3(cos(a) * 0.012, 0.16, sin(a) * 0.012), Vector3(cos(a) * 0.012, 0.186, sin(a) * 0.012), 0.002, pole, 3)


static func _hearth(st: SurfaceTool) -> void:
	for i in 8:
		var a := TAU * i / 8.0
		blob(st, Vector3(cos(a) * 0.024, 0.002, sin(a) * 0.024), Vector3(0.007, 0.005, 0.007), _c("#8a8580"), 5, 3, 0.1, float(i))
	cyl(st, Vector3(-0.015, 0.004, -0.006), Vector3(0.015, 0.006, 0.006), 0.0035, _c("#4a3326"), 4)
	cyl(st, Vector3(-0.012, 0.004, 0.012), Vector3(0.012, 0.007, -0.01), 0.0035, _c("#4a3326"), 4)


static func _fence(st: SurfaceTool) -> void:
	var w := _c("#7a5c40")
	cyl(st, Vector3(0, 0, 0), Vector3(0, 0.026, 0), 0.0022, w, 4)
	cyl(st, Vector3(0, 0.018, 0), Vector3(0.06, 0.018, 0), 0.0015, w, 3)
	cyl(st, Vector3(0, 0.009, 0), Vector3(0.06, 0.009, 0), 0.0015, w, 3)


static func _stake(st: SurfaceTool) -> void:
	cyl(st, Vector3(0, -0.01, 0), Vector3(0, 0.03, 0), 0.003, _c("#6b5138"), 4)


static func _pole(st: SurfaceTool) -> void:
	cyl(st, Vector3(0, 0, 0), Vector3(0, 0.3, 0), 0.0035, _c("#4a3326"), 5)
	blob(st, Vector3(0, 0.305, 0), Vector3(0.007, 0.007, 0.007), Color(1, 1, 1, 1), 6, 4, 0.0, 0.0)
