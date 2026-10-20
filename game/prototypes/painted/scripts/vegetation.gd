## Scatters trees, shrubs and rocks on a jittered grid, reading the painted (warped) biome from the bake
## so forests end where the painted forest wash ends. Deterministic: everything is hashed from position.
extends RefCounted

const FrameData = preload("res://scripts/frame_data.gd")
const TerrainBake = preload("res://scripts/terrain_bake.gd")
const Palette = preload("res://scripts/palette.gd")
const Geo = preload("res://scripts/geo.gd")
const Instancer = preload("res://scripts/instancer.gd")

const STEP := 0.165

# [mesh, probability per grid cell] per biome; several entries are drawn independently.
const TABLE := {
	3: [["shrub", 0.07], ["conifer", 0.02]],
	4: [["conifer", 0.85]],
	5: [["broadleaf", 0.72], ["conifer", 0.12]],
	6: [["broadleaf", 0.26], ["shrub", 0.07]],
	7: [["broadleaf", 0.018], ["shrub", 0.04]],
	8: [["acacia", 0.035], ["shrub", 0.012]],
	9: [["tropical", 0.88]],
	10: [["shrub", 0.16]],
	11: [["rock", 0.03]],
	12: [["conifer", 0.2], ["rock", 0.06]],
	13: [["rock", 0.06]],
}
const OPTS := {
	"broadleaf": {"lod": "broadleaf_lo", "lod_dist": 24.0, "far": 230.0, "sway": 0.06, "size": 0.09},
	"conifer": {"lod": "conifer_lo", "lod_dist": 24.0, "far": 230.0, "sway": 0.04, "size": 0.08},
	"tropical": {"lod": "tropical_lo", "lod_dist": 24.0, "far": 230.0, "sway": 0.05, "size": 0.1},
	"acacia": {"far": 120.0, "size": 0.1},
	"shrub": {"far": 45.0, "size": 0.04, "shadow": false},
	"rock": {"far": 60.0, "size": 0.05, "flat": 1.0},
	"fruit": {"far": 60.0, "size": 0.05, "sway": 0.04},
	"peak": {"far": 0.0, "size": 0.5, "flat": 1.0, "outline": 1.5},
}


static func h2(x: float, y: float) -> float:
	var v := sin(x * 12.9898 + y * 78.233) * 43758.5453
	return v - floorf(v)


static func place(fd: FrameData, bake: TerrainBake, inst: Instancer) -> int:
	var t0 := Time.get_ticks_msec()
	var ws := fd.world_size()
	var nx := int(ws.x / STEP)
	var nz := int(ws.y / STEP)
	var count := 0
	for iz in nz:
		for ix in nx:
			var hx := h2(ix, iz)
			var hz := h2(iz + 17.0, ix + 3.0)
			var p := Vector2((ix + 0.5 + (hx - 0.5) * 0.9) * STEP, (iz + 0.5 + (hz - 0.5) * 0.9) * STEP)
			var t := bake.painted_tile(p)
			if t < 0:
				continue
			var b: int = fd.biome[t]
			if not TABLE.has(b):
				continue
			var h := bake.raw_height(p)
			if h < 0.025:
				continue
			# groves and clearings
			var grove := Geo._vnoise(p.x * 0.45 + 3.0, p.y * 0.45 + 1.0)
			var entries: Array = TABLE[b]
			var k := 0
			for e in entries:
				k += 1
				var mesh: String = e[0]
				var prob: float = e[1]
				if b in [6, 7, 8]:
					prob *= smoothstep(0.3, 0.7, grove) * 2.0
				elif b in [4, 5, 9]:
					prob *= 0.75 + 0.35 * grove
				if b == 12:
					var mnt := bake.mountain(p)
					if mesh == "conifer":
						prob *= 1.0 - smoothstep(0.26, 0.4, h)
						prob *= 1.0 - smoothstep(0.35, 0.6, bake.normal_at(p).y * -1.0 + 1.0)
					elif mnt < 0.3:
						prob = 0.0
				if h2(ix * 1.7 + k * 13.0, iz * 2.3) >= prob:
					continue
				if bake.river_dist(p) < 0.09 or bake.trample(p) > 0.03:
					continue
				var tt := fd.tile_at(p)
				if tt >= 0 and fd.plot_kind[tt] >= 0:
					continue
				var s := 0.8 + 0.45 * h2(ix + 0.3, iz + k * 5.1)
				if mesh == "conifer" and b == 12:
					s *= 0.9
				elif mesh == "acacia":
					s *= 0.78
				var rot := h2(ix * 3.1, iz * 0.7 + k) * TAU
				var basis := Basis(Vector3.UP, rot).scaled(Vector3(s, s * (0.9 + 0.25 * h2(ix, iz + 9.0)), s))
				var y := bake.height_at(p)
				var v := h2(ix + 7.7, iz + 1.3)
				var w := h2(ix + 2.2, iz + 8.8)
				# only evergreens hold snow; leafless broadleaf crowns read as a violet-grey twig mass
				var snow_hold: float = Palette.SNOW[b] if mesh == "conifer" or mesh == "rock" else 0.0
				var var_col := Color(0.88 + 0.22 * v, 0.9 + 0.18 * (v * 0.6 + w * 0.4), 0.88 + 0.2 * w, snow_hold)
				if mesh == "rock":
					var_col = Color(0.92 + 0.1 * v, 0.9 + 0.1 * v, 0.88 + 0.1 * w, 1.0)
				inst.add(mesh, Transform3D(basis, Vector3(p.x, y, p.y)), var_col, OPTS[mesh])
				count += 1
	# mountains: two or three faceted peaks per mountain tile, a big one on peak tiles
	for t in fd.width * fd.height:
		var b: int = fd.biome[t]
		if b != FrameData.MOUNTAIN and b != FrameData.PEAK:
			continue
		var c := fd.tile_center(t)
		var n := 2 if b == FrameData.PEAK else 1 + int(h2(t, 4.0) > 0.45)
		for k in n:
			var off := Vector2(h2(t, k * 3.0 + 1.0) - 0.5, h2(t * 1.3, k * 7.0 + 2.0) - 0.5) * 0.55
			var p := c + off
			var big := b == FrameData.PEAK and k == 0
			var hgt := (0.45 + 0.75 * pow(h2(t, k + 9.0), 1.6)) * (1.45 if big else 1.0) * (0.65 if k == 1 else 1.0)
			var rad := (0.42 + 0.16 * h2(t, k + 5.0)) * (1.2 if big else 1.0)
			var basis := Basis(Vector3.UP, h2(t, k * 1.1) * TAU).scaled(Vector3(rad, hgt, rad))
			var y := bake.height_at(p) - 0.08
			var mesh := "peak%d" % ((t + k) % 3)
			inst.add(mesh, Transform3D(basis, Vector3(p.x, y, p.y)), Color(0.92 + 0.12 * h2(t, k), 0.92 + 0.1 * h2(k, t), 0.9 + 0.1 * h2(t + k, 3.0), 1.0 if big else 0.0), OPTS["peak"])
			count += 1
	# orchards: fruit trees on one continuous rotated grid, so neighbouring orchard tiles join into one orchard
	var oa := 0.35
	var od := Vector2(cos(oa), sin(oa))
	var op := Vector2(-od.y, od.x)
	for t in fd.width * fd.height:
		if fd.plot_kind[t] != 1:
			continue
		var c := fd.tile_center(t)
		var u0 := c.dot(od)
		var v0 := c.dot(op)
		for i in range(-4, 5):
			for j in range(-4, 5):
				var u := (floorf(u0 / 0.19) + i) * 0.19
				var v := (floorf(v0 / 0.21) + j) * 0.21
				var p := od * u + op * v
				if fd.tile_at(p) != t:
					continue
				if bake.trample(p) > 0.05:
					continue
				var s := 0.85 + 0.25 * h2(u, v)
				var basis := Basis(Vector3.UP, h2(v, u) * TAU).scaled(Vector3(s, s, s))
				inst.add("fruit", Transform3D(basis, Vector3(p.x, bake.height_at(p), p.y)), Color(1, 1, 1, 0.0), OPTS["fruit"])
				count += 1
	print("vegetation: %d instances in %d ms" % [count, Time.get_ticks_msec() - t0])
	return count
