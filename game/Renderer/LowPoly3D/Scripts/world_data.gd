## Loads a frame dump (src/CivSim.Harness/FrameDump.cs) and answers tile questions:
## world position of a tile, terrain height, neighbours, point-to-tile.
## World units: 1.0 = one tile column. X east, Z south (map row direction), Y up.
class_name M7WorldData
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
var claimant := PackedInt32Array()
var claim := PackedByteArray()
var visibility := PackedByteArray() # 0 unknown, 1 explored, 2 visible
var heights := PackedFloat32Array()
var camps: Array = []          # [tile, civ]
var beacons: Array = []        # [tile, civ]
var monuments := PackedInt32Array()     # stride 5: tile, civ, tick learned, population, id
var sea_tier := PackedInt32Array()       # per civilization: best boat known (0 none .. 4 outrigger)
var signal_civs := PackedInt32Array()    # per civilization: 1 when it keeps signal fires (COM-04)
var trail_meta := PackedInt32Array()     # stride 5: level, civ, trade, start, length
var trail_tiles := PackedInt32Array()    # flat tile list, -1 = unexplored or open water
var figures := PackedInt32Array()       # stride 11: x, y, civ, age, activity, flags, tool rung, settlement, id, prev x, prev y (-1 = new)
var state_bytes := PackedByteArray()    # RGBA8 per tile: owner+1, claim, visibility (0/128/255), claimant+1
var overlay_bytes := PackedByteArray()  # RGBA8 per tile: food, forecast, method rung, method fallback
var ghosts := PackedInt32Array()        # stride 8: people who just left; fade out where they stood
var herds := PackedInt32Array()         # stride 7: x, y, species, animals, id, prev x, prev y (-1 = new)
var settlements: Array = []
var plots := PackedInt32Array() # flat [tile, kind] pairs
var plot_kind := PackedInt32Array()   # -1 none, 0 field, 1 orchard
var reserved := PackedByteArray()     # tiles where wild vegetation is suppressed (settlements, plots)
var food_overlay := PackedByteArray() # 0 unknown/non-settlement, 1..4 food band
var forecast_overlay := PackedByteArray()
var method_overlay := PackedByteArray() # 0 none, 1+ best rung held by a visible/own worker
var method_fallback_overlay := PackedByteArray() # 255 where the visible worker knows a better but blocked rung

static func civ_color(civ: int) -> Color:
	if civ < 0:
		return Color(0.6, 0.6, 0.6)
	return Color(CIV_HEX[civ % 8])

func load_json(path: String) -> bool:
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		push_error("cannot open " + path)
		return false
	return load_frame_data(JSON.parse_string(f.get_as_text()), true)

## Reads the typed MatchFrame bridge payload. Static world arrays are present only
## on initial load; later payloads update viewer-filtered state in place.
func load_frame_data(value: Variant, include_world: bool = false) -> bool:
	if not value is Dictionary:
		push_error("invalid MatchFrame payload")
		return false
	var d: Dictionary = value
	if include_world:
		width = int(d.width)
		height = int(d.height)
		biome = _byte_array(d.biome)
		elevation = _byte_array(d.elevation)
		flags = _int_array(d.flags)
		plot_kind.resize(width * height)
		reserved.resize(width * height)
		_compute_heights()
	elif width == 0 or height == 0:
		push_error("MatchFrame update arrived before initial world data")
		return false
	tick = int(d.tick)
	year = int(d.year)
	season = int(d.seasonOfYear)
	food_overlay = _byte_array(d.foodOverlay) if d.has("foodOverlay") else PackedByteArray()
	forecast_overlay = _byte_array(d.forecastOverlay) if d.has("forecastOverlay") else PackedByteArray()
	method_overlay = _byte_array(d.methodOverlay) if d.has("methodOverlay") else PackedByteArray()
	method_fallback_overlay = _byte_array(d.methodFallbackOverlay) if d.has("methodFallbackOverlay") else PackedByteArray()
	claim = _byte_array(d.claim)
	visibility = _byte_array(d.visibility) if d.has("visibility") else PackedByteArray()
	if visibility.is_empty():
		visibility.resize(width * height)
		visibility.fill(2)
	owner = _int_array(d.owner)
	claimant = _int_array(d.claimant) if d.has("claimant") else owner.duplicate()
	if claim.size() != width * height or visibility.size() != width * height:
		push_error("MatchFrame tile arrays have the wrong size")
		return false
	camps.clear()
	var ct: PackedInt32Array = _int_array(d.camps)
	var cc: PackedInt32Array = _int_array(d.campCiv)
	for i in ct.size():
		camps.append([int(ct[i]), int(cc[i])])
	beacons.clear()
	if d.has("beacons"):
		var bt: PackedInt32Array = _int_array(d.beacons)
		var bc: PackedInt32Array = _int_array(d.beaconCiv)
		for i in bt.size():
			beacons.append([int(bt[i]), int(bc[i])])
	monuments = _int_data(d.monuments, 5) if d.has("monuments") else PackedInt32Array()
	sea_tier = _int_data(d.seaTier, 1) if d.has("seaTier") else PackedInt32Array()
	signal_civs = _int_data(d.signalCivs, 1) if d.has("signalCivs") else PackedInt32Array()
	trail_meta = _int_data(d.trailMeta, 5) if d.has("trailMeta") else PackedInt32Array()
	trail_tiles = _int_data(d.trailTiles, 1) if d.has("trailTiles") else PackedInt32Array()
	figures = _int_data(d.figures, 11)
	ghosts = _int_data(d.ghosts, 8) if d.has("ghosts") else PackedInt32Array()
	state_bytes = d.stateBytes if d.has("stateBytes") else PackedByteArray()
	overlay_bytes = d.overlayBytes if d.has("overlayBytes") else PackedByteArray()
	herds = _int_data(d.herds, 7)
	settlements = d.settlements
	plots = _int_data(d.plots, 2)
	plot_kind.fill(-1)
	for i in range(0, plots.size(), 2):
		plot_kind[plots[i]] = plots[i + 1]
	reserved.fill(0)
	for s in settlements:
		var t := int(s.tile)
		if t < 0 or t >= reserved.size():
			continue
		reserved[t] = 2
		for nb in neighbours(t):
			if nb >= 0 and reserved[nb] == 0:
				reserved[nb] = 1
	for i in range(0, plots.size(), 2):
		reserved[plots[i]] = 3
	return true

func _byte_array(value: Variant) -> PackedByteArray:
	if value is PackedByteArray:
		return value
	if value is String:
		return Marshalls.base64_to_raw(value)
	var result := PackedByteArray()
	for v in value:
		result.append(int(v))
	return result

func _int_array(value: Variant) -> PackedInt32Array:
	if value is PackedInt32Array:
		return value
	var result := PackedInt32Array()
	for v in value:
		result.append(int(v))
	return result

func _int_data(value: Variant, stride: int) -> PackedInt32Array:
	if value is PackedInt32Array:
		return value
	var result := PackedInt32Array()
	if value is Array and not value.is_empty() and value[0] is Array:
		for row in value:
			for v in row:
				result.append(int(v))
	else:
		for v in value:
			result.append(int(v))
	if stride > 0 and result.size() % stride != 0:
		push_error("MatchFrame packed array has an invalid stride")
	return result

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
			var dx := xx + 0.5 + off - p.x
			var dz := (yy + 0.5) * S - p.y
			var d := dx * dx + dz * dz
			if d < bd:
				bd = d
				best = yy * width + xx
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
