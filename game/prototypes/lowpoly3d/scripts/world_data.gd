## Loads a frame dump (src/CivSim.Harness/FrameDump.cs) and answers tile questions:
## world position of a tile, terrain height, neighbours, point-to-tile.
## World units: 1.0 = one tile column. X east, Z south (map row direction), Y up.
class_name WorldData
extends RefCounted

const S := 0.8660254037844386          # row spacing (sqrt(3)/2 of a column)
const R := 0.5773502691896258          # hex circumradius for a tile 1.0 wide
const MAP_UNITS := 1024.0

enum Biome { OCEAN, COAST, ICE, TUNDRA, TAIGA, TEMPERATE_FOREST, OPEN_WOODLAND, GRASSLAND, SAVANNA,
	TROPICAL_FOREST, WETLAND, DESERT, MOUNTAIN, PEAK }

enum Kind { HEARTH, LEAN_TO, HIDE_TENT, PIT_HOUSE, WATTLE_HOUSE, MUDBRICK_HOUSE, LONGHOUSE, STORAGE_PIT,
	DRYING_RACK, GRANARY, KILN, QUERN, THORN_BARRIER, LOOKOUT, WEIR, FIELD, PASTURE, ORCHARD }

const FLAG_RIVER := 1
const FLAG_LAKE := 2
const FLAG_SPRING := 4
const FLAG_SALMON := 8
const FLAG_WILDGRAIN := 16
const FLAG_COAST_ADJ := 32

const CIV_HEX := ["#E69F00", "#56B4E9", "#009E73", "#F0E442", "#0072B2", "#D55E00", "#CC79A7", "#4D4D4D"]

var width := 0
var height := 0
var tick := 0
var year := 0
var season := 0
var biome := PackedByteArray()
var elevation := PackedByteArray()
var flags := PackedInt32Array()
var owner := PackedInt32Array()
var claim := PackedByteArray()
var heights := PackedFloat32Array()
var camps: Array = []          # [tile, civ]
var figures: Array = []
var herds: Array = []
var settlements: Array = []
var plots: Array = []          # [tile, kind]
var plot_kind := PackedInt32Array()   # -1 none, 0 field, 1 orchard
var reserved := PackedByteArray()     # tiles where wild vegetation is suppressed (settlements, plots)

static func civ_color(civ: int) -> Color:
	if civ < 0:
		return Color(0.6, 0.6, 0.6)
	return Color(CIV_HEX[civ % 8])

func load_json(path: String) -> bool:
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		push_error("cannot open " + path)
		return false
	var d: Dictionary = JSON.parse_string(f.get_as_text())
	width = int(d.width)
	height = int(d.height)
	tick = int(d.tick)
	year = int(d.year)
	season = int(d.seasonOfYear)
	biome = Marshalls.base64_to_raw(d.biome)
	elevation = Marshalls.base64_to_raw(d.elevation)
	claim = Marshalls.base64_to_raw(d.claim)
	var n := width * height
	flags.resize(n)
	owner.resize(n)
	var fl: Array = d.flags
	var ow: Array = d.owner
	for i in n:
		flags[i] = int(fl[i])
		owner[i] = int(ow[i])
	camps.clear()
	var ct: Array = d.camps
	var cc: Array = d.campCiv
	for i in ct.size():
		camps.append([int(ct[i]), int(cc[i])])
	figures = d.figures
	herds = d.herds
	settlements = d.settlements
	plots = d.plots
	plot_kind.resize(n)
	plot_kind.fill(-1)
	for p in plots:
		plot_kind[int(p[0])] = int(p[1])
	reserved.resize(n)
	reserved.fill(0)
	for s in settlements:
		var t := int(s.tile)
		reserved[t] = 2
		for nb in neighbours(t):
			if nb >= 0 and reserved[nb] == 0:
				reserved[nb] = 1
	for p in plots:
		reserved[int(p[0])] = 3
	_compute_heights()
	return true

# ---------------------------------------------------------------- geometry

func tile_xy(t: int) -> Vector2i:
	return Vector2i(t % width, t / width)

func tile_center(t: int) -> Vector2:
	var x := t % width
	var y := t / width
	return Vector2(x + 0.5 + (0.5 if (y & 1) == 1 else 0.0), (y + 0.5) * S)

func map_to_world2(mx: float, my: float) -> Vector2:
	return Vector2(mx / MAP_UNITS, my / MAP_UNITS * S)

## Corner i of a pointy-top hex (i = 0 is east-north, clockwise in screen space with Z down).
static func corner(i: int) -> Vector2:
	var a := deg_to_rad(60.0 * i - 30.0)
	return Vector2(cos(a), sin(a)) * R

## Neighbour across edge i (edge i joins corner i and corner i+1).
func neighbour(t: int, i: int) -> int:
	var x := t % width
	var y := t / width
	var odd := (y & 1) == 1
	var nx := x
	var ny := y
	match i:
		0: nx = x + 1
		1:
			ny = y + 1
			nx = x + (1 if odd else 0)
		2:
			ny = y + 1
			nx = x + (0 if odd else -1)
		3: nx = x - 1
		4:
			ny = y - 1
			nx = x + (0 if odd else -1)
		5:
			ny = y - 1
			nx = x + (1 if odd else 0)
	if nx < 0 or ny < 0 or nx >= width or ny >= height:
		return -1
	return ny * width + nx

func neighbours(t: int) -> Array:
	var r := []
	for i in 6:
		r.append(neighbour(t, i))
	return r

## Tile whose centre is nearest to a world-space XZ point.
func tile_at(p: Vector2) -> int:
	var ry := int(floor(p.y / S))
	var best := -1
	var bd := 1e9
	for yy in range(ry - 1, ry + 2):
		if yy < 0 or yy >= height:
			continue
		var off := 0.5 if (yy & 1) == 1 else 0.0
		var cx := int(floor(p.x - off))
		for xx in range(cx - 1, cx + 2):
			if xx < 0 or xx >= width:
				continue
			var t := yy * width + xx
			var d := tile_center(t).distance_squared_to(p)
			if d < bd:
				bd = d
				best = t
	return best

func height_at(p: Vector2) -> float:
	var t := tile_at(p)
	return heights[t] if t >= 0 else -0.6

func is_water(t: int) -> bool:
	return t < 0 or biome[t] <= Biome.COAST

func is_land(t: int) -> bool:
	return t >= 0 and biome[t] > Biome.COAST

# ---------------------------------------------------------------- heights

const STEP := 0.1

func _compute_heights() -> void:
	var n := width * height
	heights.resize(n)
	for t in n:
		heights[t] = base_height(t)
	# beaches: land next to the sea sits low so the shore reads as a gentle step
	for t in n:
		if biome[t] > Biome.COAST and biome[t] < Biome.MOUNTAIN:
			for nb in neighbours(t):
				if nb >= 0 and biome[nb] <= Biome.COAST:
					heights[t] = minf(heights[t], 0.08)
					break

func base_height(t: int) -> float:
	var b := biome[t]
	var e := float(elevation[t])
	if b == Biome.OCEAN:
		return -0.7
	if b == Biome.COAST:
		return -0.14
	if b == Biome.MOUNTAIN:
		return snappedf(1.0 + (e - 183.0) / 42.0 * 0.3, STEP)
	if b == Biome.PEAK:
		return snappedf(1.3 + (e - 226.0) / 14.0 * 0.2, STEP)
	var lv := 0.08 + clampf((e - 70.0) / 112.0, 0.0, 1.0) * 0.85
	return maxf(0.08, snappedf(lv, STEP))

## Deterministic hash in [0,1) for procedural variety.
static func hash01(a: int, b: int = 0, c: int = 0) -> float:
	var h := (a * 374761393 + b * 668265263 + c * 2147483647 + 1013904223) & 0x7fffffff
	h = ((h ^ (h >> 13)) * 1274126177) & 0x7fffffff
	h = h ^ (h >> 16)
	return float(h & 0xffffff) / 16777216.0
