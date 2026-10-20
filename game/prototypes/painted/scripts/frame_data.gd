## One match frame as dumped by `civsim-harness view --dump-frame` (src/CivSim.Harness/FrameDump.cs).
## Positions are converted from map units to world units: one tile column = 1.0, rows sqrt(3)/2 apart.
extends RefCounted

const UNIT := 1024.0
const ROW := 0.8660254

const OCEAN := 0
const COAST := 1
const ICE := 2
const TUNDRA := 3
const TAIGA := 4
const TEMPERATE := 5
const WOODLAND := 6
const GRASSLAND := 7
const SAVANNA := 8
const TROPICAL := 9
const WETLAND := 10
const DESERT := 11
const MOUNTAIN := 12
const PEAK := 13

const F_RIVER := 1
const F_LAKE := 2
const F_SPRING := 4
const F_SALMON := 8
const F_GRAIN := 16
const F_COASTADJ := 32

var name := ""
var width := 0
var height := 0
var tick := 0
var year := 0
var season := 0
var biome := PackedByteArray()
var elevation := PackedByteArray()
var claim := PackedByteArray()
var flags := PackedInt32Array()
var owner := PackedInt32Array()
var plot_kind := PackedInt32Array() # -1 none, 0 field, 1 orchard
var camps: Array = [] # {pos: Vector2, civ: int, tile: int}
var figures: Array = [] # {pos: Vector2, civ, age, activity, flags, tool, settlement}
var herds: Array = [] # {pos: Vector2, species, animals}
var settlements: Array = [] # {tile, pos: Vector2, civ, population, food, structures: Array}


static func load_file(path: String):
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		push_error("cannot open %s" % path)
		return null
	var d: Dictionary = JSON.parse_string(f.get_as_text())
	var fd = load("res://scripts/frame_data.gd").new()
	fd.name = path.get_file().get_basename()
	fd.width = int(d["width"])
	fd.height = int(d["height"])
	fd.tick = int(d["tick"])
	fd.year = int(d["year"])
	fd.season = int(d["seasonOfYear"])
	fd.biome = Marshalls.base64_to_raw(d["biome"])
	fd.elevation = Marshalls.base64_to_raw(d["elevation"])
	fd.claim = Marshalls.base64_to_raw(d["claim"])
	var n: int = fd.width * fd.height
	fd.flags.resize(n)
	fd.owner.resize(n)
	fd.plot_kind.resize(n)
	var fl: Array = d["flags"]
	var ow: Array = d["owner"]
	for i in n:
		fd.flags[i] = int(fl[i])
		fd.owner[i] = int(ow[i])
		fd.plot_kind[i] = -1
	for p in d["plots"]:
		fd.plot_kind[int(p[0])] = int(p[1])
	var camp_tiles: Array = d["camps"]
	var camp_civ: Array = d["campCiv"]
	for i in camp_tiles.size():
		var t := int(camp_tiles[i])
		fd.camps.append({"tile": t, "pos": fd.tile_center(t), "civ": int(camp_civ[i])})
	for g in d["figures"]:
		fd.figures.append({
			"pos": map_to_world(g[0], g[1]), "civ": int(g[2]), "age": int(g[3]), "activity": int(g[4]),
			"flags": int(g[5]), "tool": int(g[6]), "settlement": int(g[7])})
	for h in d["herds"]:
		fd.herds.append({"pos": map_to_world(h[0], h[1]), "species": int(h[2]), "animals": int(h[3])})
	for s in d["settlements"]:
		var t := int(s["tile"])
		fd.settlements.append({
			"tile": t, "pos": fd.tile_center(t), "civ": int(s["civ"]), "population": int(s["population"]),
			"food": int(s["food"]), "structures": s["structures"]})
	return fd


static func map_to_world(mx, my) -> Vector2:
	return Vector2(float(mx) / UNIT, float(my) / UNIT * ROW)


func tile_center(t: int) -> Vector2:
	var x := t % width
	var y := t / width
	return Vector2(x + 0.5 + (0.5 if (y & 1) == 1 else 0.0), (y + 0.5) * ROW)


func world_size() -> Vector2:
	return Vector2(width + 0.5, height * ROW)


func is_water(t: int) -> bool:
	return biome[t] == OCEAN or biome[t] == COAST


## Nearest tile to a world position (true hex metric), or -1 off the map.
func tile_at(p: Vector2) -> int:
	var rf := (p.y - 0.5 * ROW) / ROW
	var qf := (p.x - 0.5) - rf * 0.5
	# cube rounding
	var x := qf
	var z := rf
	var y := -x - z
	var rx := roundf(x)
	var ry := roundf(y)
	var rz := roundf(z)
	var dx := absf(rx - x)
	var dy := absf(ry - y)
	var dz := absf(rz - z)
	if dx > dy and dx > dz:
		rx = -ry - rz
	elif dy > dz:
		ry = -rx - rz
	else:
		rz = -rx - ry
	var r := int(rz)
	var q := int(rx)
	var col := q + (r - (r & 1)) / 2
	if r < 0 or r >= height or col < 0 or col >= width:
		return -1
	return r * width + col


## Neighbours of tile t in odd-r offset coordinates (pointy-top, odd rows shifted east).
func neighbors(t: int) -> PackedInt32Array:
	var x := t % width
	var y := t / width
	var res := PackedInt32Array()
	var odd := (y & 1) == 1
	var dirs := [[1, 0], [-1, 0], [0, -1], [0, 1], [1 if odd else -1, -1], [1 if odd else -1, 1]]
	for dd in dirs:
		var nx: int = x + dd[0]
		var ny: int = y + dd[1]
		if nx >= 0 and ny >= 0 and nx < width and ny < height:
			res.append(ny * width + nx)
	return res
