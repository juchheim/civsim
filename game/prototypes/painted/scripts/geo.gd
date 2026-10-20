## Line work extracted from the tile data: rivers (tree from mouth to source) and territory borders
## (closed loops along hex edges, smoothed into hand-drawn curves). Pure data; ribbons are built in lines.gd.
extends RefCounted

const FrameData = preload("res://scripts/frame_data.gd")

const S := 0.5773503 # hex circumradius for unit column width


static func _hash(x: float, y: float) -> float:
	var h := sin(x * 127.1 + y * 311.7) * 43758.5453
	return h - floorf(h)


static func _vnoise(x: float, y: float) -> float:
	var ix := floorf(x)
	var iy := floorf(y)
	var fx := x - ix
	var fy := y - iy
	var ux := fx * fx * (3.0 - 2.0 * fx)
	var uy := fy * fy * (3.0 - 2.0 * fy)
	var a := _hash(ix, iy)
	var b := _hash(ix + 1, iy)
	var c := _hash(ix, iy + 1)
	var d := _hash(ix + 1, iy + 1)
	return lerpf(lerpf(a, b, ux), lerpf(c, d, ux), uy)


static func chaikin(pts: PackedVector2Array, closed: bool, iterations: int) -> PackedVector2Array:
	var cur := pts
	for _it in iterations:
		var out := PackedVector2Array()
		var n := cur.size()
		if n < 3:
			return cur
		if not closed:
			out.append(cur[0])
		var last := n if closed else n - 1
		for i in last:
			var a := cur[i]
			var b := cur[(i + 1) % n]
			out.append(a.lerp(b, 0.25))
			out.append(a.lerp(b, 0.75))
		if not closed:
			out.append(cur[n - 1])
		cur = out
	return cur


## Moving average over a closed loop: irons the one-tile hex zigzag out of a border.
static func smooth_loop(pts: PackedVector2Array, half: int) -> PackedVector2Array:
	var n := pts.size()
	if n < half * 2 + 3:
		return pts
	var out := PackedVector2Array()
	out.resize(n)
	for i in n:
		var acc := Vector2.ZERO
		for k in range(-half, half + 1):
			acc += pts[(i + k + n) % n]
		out[i] = acc / float(half * 2 + 1)
	return out


static func resample(pts: PackedVector2Array, step: float, closed: bool) -> PackedVector2Array:
	var out := PackedVector2Array()
	if pts.size() < 2:
		return pts
	var src := pts.duplicate()
	if closed:
		src.append(pts[0])
	out.append(src[0])
	var carry := 0.0
	for i in src.size() - 1:
		var a := src[i]
		var b := src[i + 1]
		var L := a.distance_to(b)
		var t := step - carry
		while t <= L:
			out.append(a.lerp(b, t / L))
			t += step
		carry = L - (t - step)
	if not closed and out[out.size() - 1].distance_to(src[src.size() - 1]) > step * 0.3:
		out.append(src[src.size() - 1])
	return out


## Perpendicular wobble so straight runs of river tiles meander like a hand-drawn line.
static func meander(pts: PackedVector2Array, amp: float, freq: float, seed_v: float) -> PackedVector2Array:
	var out := PackedVector2Array()
	var n := pts.size()
	var arc := 0.0
	for i in n:
		if i > 0:
			arc += pts[i].distance_to(pts[i - 1])
		var a := pts[maxi(i - 1, 0)]
		var b := pts[mini(i + 1, n - 1)]
		var d := (b - a).normalized()
		var perp := Vector2(-d.y, d.x)
		var fade := minf(1.0, minf(arc, 99.0) / 0.6)
		var w := (_vnoise(arc * freq + seed_v, seed_v * 3.1) - 0.5) * 2.0 + (_vnoise(arc * freq * 2.7, seed_v + 5.0) - 0.5) * 0.6
		out.append(pts[i] + perp * w * amp * fade)
	return out


## Returns [{pts: PackedVector2Array, widths: PackedFloat32Array}] in world units.
static func rivers(fd: FrameData) -> Array:
	var n := fd.width * fd.height
	var is_r := PackedByteArray()
	is_r.resize(n)
	for t in n:
		is_r[t] = 1 if (fd.flags[t] & FrameData.F_RIVER) != 0 and not fd.is_water(t) else 0
	var parent := PackedInt32Array()
	parent.resize(n)
	parent.fill(-2)
	var order: Array[int] = []
	var mouth_pt := {}
	# BFS from mouths (river tiles touching water), then from the lowest tile of any leftover component
	var queue: Array[int] = []
	for t in n:
		if is_r[t] == 0:
			continue
		for nb in fd.neighbors(t):
			if fd.is_water(nb):
				parent[t] = -1
				queue.append(t)
				mouth_pt[t] = fd.tile_center(t).lerp(fd.tile_center(nb), 0.75)
				break
	while true:
		while not queue.is_empty():
			var t: int = queue.pop_front()
			order.append(t)
			for nb in fd.neighbors(t):
				if is_r[nb] == 1 and parent[nb] == -2:
					parent[nb] = t
					queue.append(nb)
		var best := -1
		for t in n:
			if is_r[t] == 1 and parent[t] == -2 and (best < 0 or fd.elevation[t] < fd.elevation[best]):
				best = t
		if best < 0:
			break
		parent[best] = -1
		queue.append(best)
	# upstream counts
	var up := PackedInt32Array()
	up.resize(n)
	var children := PackedInt32Array()
	children.resize(n)
	for i in range(order.size() - 1, -1, -1):
		var t: int = order[i]
		up[t] += 1
		if parent[t] >= 0:
			up[parent[t]] += up[t]
			children[parent[t]] += 1
	var visited := {}
	var out := []
	# walk from each leaf to the mouth, stopping at a node already drawn (the junction)
	for t in order:
		if children[t] != 0:
			continue
		var pts := PackedVector2Array()
		var ws := PackedFloat32Array()
		var cur := t
		while cur >= 0:
			pts.append(fd.tile_center(cur))
			ws.append(up[cur])
			if visited.has(cur):
				break
			visited[cur] = true
			if parent[cur] == -1 and mouth_pt.has(cur):
				pts.append(mouth_pt[cur])
				ws.append(up[cur])
			cur = parent[cur]
		if pts.size() < 2:
			continue
		var sm := chaikin(pts, false, 2)
		sm = resample(sm, 0.08, false)
		sm = meander(sm, 0.13, 0.9, float(t % 97))
		var wsm := PackedFloat32Array()
		for i in sm.size():
			var f := float(i) / maxf(1.0, sm.size() - 1.0)
			var u := lerpf(ws[0], ws[ws.size() - 1], f)
			var taper := smoothstep(0.0, 0.9, float(i) * 0.08)
			wsm.append((0.012 + 0.009 * sqrt(u)) * (0.25 + 0.75 * taper))
		out.append({"pts": sm, "widths": wsm})
	return out


static func segments(lines: Array) -> PackedFloat32Array:
	var seg := PackedFloat32Array()
	for l in lines:
		var pts: PackedVector2Array = l["pts"]
		for i in pts.size() - 1:
			seg.append_array([pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y])
	return seg


static func _corner(c: Vector2, i: int) -> Vector2:
	var a := deg_to_rad(60.0 * i - 30.0)
	return c + Vector2(cos(a), sin(a)) * S


static func _neighbor_dir(fd: FrameData, t: int, i: int) -> int:
	var x := t % fd.width
	var y := t / fd.width
	var odd := (y & 1) == 1
	var sh := 1 if odd else 0
	var dx := [1, sh, sh - 1, -1, sh - 1, sh]
	var dy := [0, 1, 1, 0, -1, -1]
	var nx: int = x + dx[i]
	var ny: int = y + dy[i]
	if nx < 0 or ny < 0 or nx >= fd.width or ny >= fd.height:
		return -1
	return ny * fd.width + nx


static func _key(p: Vector2) -> Vector2i:
	# hex corners lie on an exact lattice: x in halves, z in steps of S/2
	# (row centres sit at 1.5 lattice steps, so corners land on half steps: shift before rounding)
	return Vector2i(roundi(p.x * 2.0), roundi(p.y / (S * 0.5) - 0.5))


## Returns [{civ, pts (closed loop, smoothed, offset inward by `inset`)}].
static func borders(fd: FrameData, inset: float) -> Array:
	var out := []
	var civs := {}
	for t in fd.width * fd.height:
		if fd.owner[t] >= 0:
			civs[fd.owner[t]] = true
	for civ in civs.keys():
		var next := {} # start corner key -> [start, end]
		for t in fd.width * fd.height:
			if fd.owner[t] != civ:
				continue
			var c := fd.tile_center(t)
			for i in 6:
				var nb := _neighbor_dir(fd, t, i)
				if nb >= 0 and fd.owner[nb] == civ:
					continue
				var a := _corner(c, i)
				var b := _corner(c, (i + 1) % 6)
				next[_key(a)] = [a, b]
		while not next.is_empty():
			var k0: Vector2i = next.keys()[0]
			var loop := PackedVector2Array()
			var k := k0
			var guard := 0
			while next.has(k) and guard < 100000:
				var e: Array = next[k]
				next.erase(k)
				loop.append(e[0])
				k = _key(e[1])
				guard += 1
			if loop.size() < 3:
				continue
			# drop the outline of tiny holes (one or two unowned tiles) so interiors stay calm
			var area := 0.0
			for i in loop.size():
				var a2 := loop[i]
				var b2 := loop[(i + 1) % loop.size()]
				area += a2.x * b2.y - b2.x * a2.y
			area *= 0.5
			if area < 0.0 and absf(area) < 1.9:
				continue
			var sm := chaikin(loop, true, 3)
			# inward offset: interior is on the left normal (-d.y, d.x) of the walking direction
			var off := PackedVector2Array()
			var m := sm.size()
			for i in m:
				var d := (sm[(i + 1) % m] - sm[(i - 1 + m) % m]).normalized()
				off.append(sm[i] + Vector2(-d.y, d.x) * inset)
			out.append({"civ": civ, "pts": smooth_loop(resample(off, 0.05, true), 6)})
	return out
