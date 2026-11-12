## The default settlement core (docs/MVP/31 §4, work packages WP1, WP2 and WP6c), drawn from simulation claims.
##
## Every live settlement is drawn on a fixed slot map of its seven-hex core (its own hex and the six around it) instead of the sunflower
## spiral of world_builder.gd `_settlements`. Each core hex has seven sub-slots: s0 at its centre and petals s1..s6 at PETAL from it (petal
## i + 1 toward hex direction i). The centre hex is the Heart; the six around it are quarters named by their direction from the Front
## (Front, Homes A, Homes B, Workshops, Watch, Sacred, §4.2). What a settlement shows grows with its stage (Band, Village, Town, City,
## §4.3); when a quarter is full the overflow rules of §4.4 apply (upgrade-replace chains, merges, compressed homes, dropped dressing), and
## nothing is drawn outside the core except the outer works of §4.7. Upgrade chains (§4.5) draw only their best tier: one perimeter line
## (thorn hedge, palisade, stone wall I to III, gatehouse), one food store, one sacred building, one council seat, one watch post, one
## pottery yard, one metal yard, one water work. Knowledge props that duplicate built structures (totem, shrine, signal, palisade) are not
## drawn; the other knowledge props are dressing beside their host building (§4.6).
##
## The simulation enforces site, ring-1 and outer claims. A plot can remain on a newly claimed hex until the next-season release; the
## renderer hides it there until the simulation releases it, and never puts a garden cue on a claimed hex.
##
## `--layout-dump=FILE` writes every settlement prop with its hex, sub-slot, footprint and chain tier to FILE (JSON) each time the
## settlements are laid out; game/tools/check_core_layout.py checks it.
class_name M7CoreLayout
extends RefCounted

const PETAL := 0.29          # from a hex centre to a petal sub-slot
const SMALL_R := 0.135       # footprint radius of a one-sub-slot item (house, rack, quern, totem, lookout)
const HALF_R := 0.19         # a half-hex item: s0 and two adjacent petals (granary, pottery yard, forge yard, drill ground)
const HALF_OFF := 0.15       # from the hex centre to a half-hex item, toward the corner between its two petals
const WHOLE_R := 0.42        # a whole-hex building (temple, academy, castle, Wonder)
const FRONT_WHOLE_R := 0.36  # a whole-hex building in the Front quarter keeps clear of the gate
const HEART3_OFF := 0.32     # the Heart landmark (chief's hall) stands behind the hearth on three petals
const HEART3_R := 0.2
const PERIM := 0.465         # from a hex centre to the perimeter line (0.035 inside the hex boundary)
const PERIM_THORN := 0.48    # the thorn hedge is bushier and stands a little farther out
const CORE_VS := 1.25        # village scale for core items (the spiral used 1.45)
const RIVER_CLEAR := 0.05    # a sub-slot keeps this much more than its footprint from a river's middle
const GARDEN_OPEN_R := 0.22
const GARDEN_PETAL_R := 0.12
const GARDEN_GAP_R := 0.06
const FIELD_R := 0.642       # the field mesh's footprint radius at scale 1
const WALL_SCALE := 1.2
const SEG := 0.16            # art_defense.gd SEG: the wall run mesh's length

enum Q { HEART, FRONT, HOMES_A, HOMES_B, WORKS, WATCH, SACRED, CITADEL, LEARNING, CIVIC, HEALTH, WONDER_A, WONDER_B, WONDER_C, HOMES_C, HOMES_D }
const QN := 16
const Q_NAMES := ["heart", "front", "homes_a", "homes_b", "workshops", "watch", "sacred",
	"citadel", "learning", "civic", "health", "wonder_a", "wonder_b", "wonder_c", "homes_c", "homes_d"]
const Q_OFFSET := [0, 0, 1, -1, 2, -2, 3]
const VILLAGE_GROUND := 1.2  # terrain village-ground radius round every settlement hearth
const RING2_GROUND := 0.5    # and round each ring-2 hex's centre
## Quarters that move to the next free hex when theirs is water, mountain or off the map (§4.2); the Front stays on its water.
const REASSIGN := [Q.HOMES_A, Q.WORKS, Q.SACRED, Q.HOMES_B, Q.WATCH]
const STAGES := ["band", "village", "town", "city"]
## Kinds that may stand outside the core (§4.7).
const OUTER_KINDS := ["paddock", "water_mill", "marker", "charcoal", "quarry", "aqueduct_span", "lighthouse", "frontier_wall", "siege"]
## Knowledge props (DecorTable) that are dressing beside a host, with the quarters the host stands in.
const DRESSING := {
	"hideframe": [Q.HOMES_A, Q.HOMES_B, Q.HEART], "loom": [Q.HOMES_A, Q.HOMES_B, Q.HEART], "woolbales": [Q.HOMES_B, Q.HOMES_A, Q.WORKS, Q.HEART],
	"pots": [Q.WORKS, Q.HEART], "bricks": [Q.WORKS, Q.HEART], "baskets": [Q.WORKS, Q.HEART], "woodwork": [Q.WORKS, Q.HEART],
	"sledge": [Q.WORKS, Q.HEART], "grave": [Q.SACRED, Q.HEART],
}
## Knowledge props that duplicate a built structure: never drawn in the core layout (§4.6).
const DUPLICATES := ["totem", "shrine", "signal", "palisade"]

var wb: M7WorldBuilder       # owns the props batcher and the site lists the rigs read
var w: M7WorldData
var gallery := {}            # coreGallery name -> Vector3 (hearth), for the capture scenario
var garden_cues := {}        # plot tile -> [position, scale] for world_builder._draw_plots
var _rows: Array = []        # the layout dump, one row per settlement
var _hd := {}                # mesh -> [footprint radius, AABB centre (x, z)] at scale 1
var _sim_core_tiles := {}     # tiles claimed by any live settlement's site and ring 1
var _sim_outer_tiles := {}    # tiles claimed by any live settlement's sim-computed outer districts
var _sim_claimed_tiles := {}  # union of all living settlements' core and outer claims

# the settlement being laid out
var _si := 0
var _t := 0
var _civ := 0
var _c := Vector2.ZERO       # the hearth
var _c0 := Vector2.ZERO      # the site tile's centre
var _h0 := 0.0
var _seed := 0.0
var _cc := Color.WHITE
var _stage := 1
var _front := 0
var _water_front := false
var _hex: Array = []         # 0 = centre, 1 + d = ring-1 hex in direction d: {tile, c, dir, land, use, slots}
var _qhex: Array = []        # per quarter: the hex index it holds at this stage, or -1
var _qhex_all: Array = []    # per quarter: the hex index it would hold (the dump shows open ground too)
var _items: Array = []
var _dropped: Array = []
var _group := 0
var _ring2: Array = []       # this settlement's sim rows: [tile, district, position, renderer hex index]
var _unhoused_quarters := {} # renderer quarter -> true for present districts without a sim-assigned tile
var _gate_i := -2            # selected perimeter edge; reused after the early slot reservation
var _whole_n := 0            # whole-hex districts beyond the Sacred one (the staged rule's count)

func _init(builder: M7WorldBuilder) -> void:
	wb = builder
	w = builder.w

## Called once per layout pass, before the settlements.
func begin() -> void:
	_rows.clear()
	gallery.clear()
	garden_cues.clear()
	_group = 0
	_sim_core_tiles.clear()
	_sim_outer_tiles.clear()
	_sim_claimed_tiles.clear()
	for s in w.settlements:
		if bool(s.get("remembered")) or int(s.get("population", 0)) <= 0:
			continue
		var st := int(s.tile)
		_sim_core_tiles[st] = true
		_sim_claimed_tiles[st] = true
		for d in 6:
			var nb := w.neighbour(st, d)
			if nb >= 0:
				_sim_core_tiles[nb] = true
				_sim_claimed_tiles[nb] = true
		for row in s.get("coreOuter", []):
			var tile := int(row[0])
			if tile >= 0:
				_sim_outer_tiles[tile] = true
				_sim_claimed_tiles[tile] = true

# ------------------------------------------------------------------ simulation-claimed outer hexes (docs/MVP/31b §3.2)

## Pre-pass for world_builder (before the vegetation): the frame's claimed outer tiles, so no trees grow under them.
func reserved_ring2() -> Array:
	begin()
	var out := []
	for s in w.settlements:
		if bool(s.get("remembered")) or int(s.get("population", 0)) <= 0:
			continue
		for row in s.get("coreOuter", []):
			if int(row[0]) >= 0:
				out.append(int(row[0]))
	return out

func _quarter_for_district(district: int) -> int:
	if district < 0 or district >= w.core_district_ids.size():
		return -1
	match String(w.core_district_ids[district]):
		"Ziggurat": return Q.WONDER_B
		"GreatDome": return Q.WONDER_A
		"GreatLibrary": return Q.WONDER_C
		"Citadel": return Q.CITADEL
		"Learning": return Q.LEARNING
		"Civic": return Q.CIVIC
		"Health": return Q.HEALTH
		"HomesC": return Q.HOMES_C
		"HomesD": return Q.HOMES_D
	return -1

func _district_name(district: int) -> String:
	return String(w.core_district_ids[district]) if district >= 0 and district < w.core_district_ids.size() else "district_%d" % district

func _expected_whole_items(s: Dictionary) -> Array:
	var expected := []
	for row in _ring2:
		if int(row[0]) < 0:
			continue
		var district := int(row[1])
		var name := _district_name(district)
		var item := ""
		match name:
			"Ziggurat":
				if wb.zig_stage(s) > 0: item = "ziggurat"
			"GreatDome":
				if w.iron_works(s, "great_dome") > 0: item = "dome"
			"GreatLibrary":
				if w.iron_works(s, "great_library") > 0: item = "library"
			"Citadel":
				if wb.castle_stage(s) > 0: item = "castle"
			"Learning":
				if w.iron_works(s, "academy") > 0: item = "academy"
			"Civic":
				if w.iron_works(s, "theatre_arena") > 0: item = "theatre"
			"Health":
				if w.iron_works(s, "bathhouse") > 0: item = "bathhouse"
		if item != "":
			expected.append([district, item])
	return expected

## The stage a settlement is drawn at: Band, Village, Town or City (0..3). The frame does not carry the sim's tier (LivingEconomyModel
## Civic().Tier), so it is read from what the renderer sees: no permanent house and under 20 people is a Band; 90 people and a temple or
## ziggurat a City (CityMinPop, SOC-09's structure); 60 people a Town (TownMinPop; the Market is not in the frame). The capture gallery
## forces a stage with "coreStage".
func stage_of(s: Dictionary) -> int:
	var forced := String(s.get("coreStage", ""))
	if forced != "":
		return maxi(0, STAGES.find(forced))
	var K := M7WorldData.Kind
	var st: Array = s.structures
	var permanent := int(st[K.PIT_HOUSE]) + int(st[K.WATTLE_HOUSE]) + int(st[K.MUDBRICK_HOUSE]) + int(st[K.LONGHOUSE]) + int(s.get("halls", 0))
	var pop := int(s.population)
	if permanent == 0 and pop < 20:
		return 0
	if pop >= 90 and (int(s.get("temples", 0)) > 0 or wb.zig_stage(s) > 0):
		return 3
	if pop >= 60:
		return 2
	return 1

## Maximum house meshes that could fit before other core items take their slots. The frame digest uses this natural ceiling so
## additional population or house counts stop invalidating the village after every possible home quarter is full.
func dwelling_capacity(s: Dictionary) -> int:
	var stage := stage_of(s)
	if stage == 0:
		return 5 # the Heart has five usable petals after the hearth and way in
	if stage == 1:
		return 10 # Homes A plus the Village's three Heart petals
	var slots := 14 # Homes A and B in ring 1
	for row in s.get("coreOuter", []):
		if int(row[0]) < 0:
			continue
		var district := _district_name(int(row[1]))
		if district == "HomesC" or district == "HomesD":
			slots += 7
	return slots * (3 if stage == 3 else 2)

static func _dir(d: int) -> Vector2:
	var a := deg_to_rad(60.0 * posmod(d, 6))
	return Vector2(cos(a), sin(a))

func _face(at: Vector2) -> float:
	var d := _c - at
	if d.length() < 0.001:
		return _seed * TAU
	return atan2(d.x, d.y)

func _foot(mesh_name: String) -> Array:
	if not _hd.has(mesh_name):
		var m: ArrayMesh = wb.kit.meshes.get(mesh_name)
		if m == null:
			_hd[mesh_name] = [0.1, Vector2.ZERO, Vector2(0.07, 0.07)]
		else:
			var a := m.get_aabb()
			_hd[mesh_name] = [Vector2(a.size.x, a.size.z).length() * 0.5, Vector2(a.position.x + a.size.x * 0.5, a.position.z + a.size.z * 0.5),
				Vector2(a.size.x, a.size.z) * 0.5]
	return _hd[mesh_name]

func _tint(k: int) -> Color:
	return Color(_cc.r, _cc.g, _cc.b, M7WorldData.hash01(_t, k, 6))

# ------------------------------------------------------------------ slots

func _slot_pos(hi: int, k: int) -> Vector2:
	var hx: Dictionary = _hex[hi]
	if k == 0:
		return hx.c
	return (hx.c as Vector2) + _dir(k - 1) * PETAL

func _build_hexes() -> void:
	_hex.clear()
	for hi in 7:
		var tile := _t if hi == 0 else w.neighbour(_t, hi - 1)
		var centre := _c0 if hi == 0 else _c0 + _dir(hi - 1) * 1.0
		var land := tile >= 0 and w.is_land(tile) and w.biome[tile] < M7WorldData.Biome.MOUNTAIN
		if hi == 0:
			land = true
		var slots := [0, 0, 0, 0, 0, 0, 0]     # 0 free, 1 taken, 2 kept clear (the way in), 3 unusable (water, mountain, river)
		for k in 7:
			var p := centre if k == 0 else centre + _dir(k - 1) * PETAL
			var tp := w.tile_at(p)
			var ok := land and tp >= 0 and w.is_land(tp) and w.biome[tp] < M7WorldData.Biome.MOUNTAIN
			if ok and wb._river_dist(p) < SMALL_R + RIVER_CLEAR:
				ok = false
			slots[k] = 0 if ok else 3
		_hex.append({"tile": tile, "c": centre, "dir": hi - 1, "land": land, "use": false, "slots": slots})
	# Outer rows are supplied by the frame. Keep the full list for the dump, including unhoused districts.
	for r in _ring2:
		r[3] = -1
		if int(r[0]) < 0:
			continue
		var centre := w.tile_center(int(r[0]))
		var slots := [0, 0, 0, 0, 0, 0, 0]
		for k in 7:
			var p := centre if k == 0 else centre + _dir(k - 1) * PETAL
			var tp := w.tile_at(p)
			var ok := tp >= 0 and w.is_land(tp) and w.biome[tp] < M7WorldData.Biome.MOUNTAIN and wb._river_dist(p) >= SMALL_R + RIVER_CLEAR
			slots[k] = 0 if ok else 3
		var ang := atan2((centre - _c0).y, (centre - _c0).x)
		var hi := _hex.size()
		_hex.append({"tile": int(r[0]), "c": centre, "dir": posmod(roundi(ang / (PI / 3.0)), 6), "land": true, "use": false, "slots": slots})
		r[3] = hi

func _assign_quarters() -> void:
	_qhex_all = []
	_qhex_all.resize(QN)
	_qhex_all.fill(-1)
	_qhex_all[Q.HEART] = 0
	var taken := {}
	if not _water_front and _hex[_front + 1].land:
		_qhex_all[Q.FRONT] = _front + 1
		taken[_front] = true
	for q in REASSIGN:
		var d := posmod(_front + int(Q_OFFSET[q]), 6)
		if _hex[d + 1].land and not taken.has(d):
			_qhex_all[q] = d + 1
			taken[d] = true
	for q in REASSIGN:
		if _qhex_all[q] >= 0:
			continue
		var nat := _front + int(Q_OFFSET[q])
		for k in [1, -1, 2, -2, 3]:
			var d := posmod(nat + int(k), 6)
			if _hex[d + 1].land and not taken.has(d):
				_qhex_all[q] = d + 1
				taken[d] = true
				break
	# The simulation assigns each district to its exact tile and angular position.
	_unhoused_quarters.clear()
	for r in _ring2:
		var q := _quarter_for_district(int(r[1]))
		if q < 0:
			continue
		if int(r[0]) < 0:
			_unhoused_quarters[q] = true
			_dropped.append("no_room_" + _district_name(int(r[1])))
		else:
			_qhex_all[q] = int(r[3])
	for hi in range(7, _hex.size()):
		_hex[hi].use = true
	_qhex = _qhex_all.duplicate()
	if _stage == 0:
		for q in QN:
			if q != Q.HEART:
				_qhex[q] = -1
	elif _stage == 1:
		for q in [Q.FRONT, Q.HOMES_B, Q.WATCH]:
			_qhex[q] = -1
	# the perimeter: the centre hex for a Band, the whole core (every land hex) from a Village on, so a wall never has to be redrawn bigger
	_hex[0].use = true
	if _stage >= 1:
		for d in 6:
			if _hex[d + 1].land:
				_hex[d + 1].use = true

func _quarter_of(hi: int) -> String:
	for q in QN:
		if _qhex_all[q] == hi:
			return Q_NAMES[q]
	return "open"

## Petals of a hex in the order they fill: on the Heart from the back (away from the Front); on a quarter from its s0 and the petal toward
## the Heart, round to the outermost petal.
func _petal_order(hi: int) -> Array:
	if hi == 0:
		return [_front + 3, _front + 2, _front + 4, _front + 1, _front + 5].map(func(d): return posmod(d, 6) + 1)
	var d := int(_hex[hi].dir)
	return [0] + [d + 3, d + 2, d + 4, d + 1, d + 5, d].map(func(e): return posmod(e, 6) + 1)

func _take_small(prefs: Array) -> Array:
	for q in prefs:
		var hi: int = _qhex[q]
		if hi < 0:
			continue
		for k in _petal_order(hi):
			if _hex[hi].slots[k] == 0:
				_hex[hi].slots[k] = 1
				return [hi, k]
	return []

func _free_small(prefs: Array) -> int:
	var n := 0
	for q in prefs:
		var hi: int = _qhex[q]
		if hi < 0:
			continue
		for k in 7:
			if _hex[hi].slots[k] == 0:
				n += 1
	return n

## A half-hex place: s0 and two adjacent petals. Returns [hex, first petal's direction] or [].
func _take_half(prefs: Array) -> Array:
	for q in prefs:
		var hi: int = _qhex[q]
		if hi < 0 or hi == 0 or _hex[hi].slots[0] != 0:
			continue
		var d := int(_hex[hi].dir)
		for j in [d + 3, d + 2, d + 4, d + 1, d + 5, d]:
			var a := posmod(j, 6) + 1
			var b := posmod(j + 1, 6) + 1
			if _hex[hi].slots[a] == 0 and _hex[hi].slots[b] == 0:
				_hex[hi].slots[0] = 1
				_hex[hi].slots[a] = 1
				_hex[hi].slots[b] = 1
				return [hi, posmod(j, 6)]
	return []

func _take_whole(prefs: Array) -> int:
	for q in prefs:
		var hi: int = _qhex[q]
		if hi <= 0:
			continue
		var outer := hi >= 7
		var ok := true
		for k in 7:
			var v: int = _hex[hi].slots[k]
			if v == 1 or (v == 3 and not outer) or (v == 2 and q != Q.FRONT):
				ok = false
				break
		if ok:
			for k in 7:
				_hex[hi].slots[k] = 1
			return hi
	return -1

# ------------------------------------------------------------------ drawing and the dump

func _record(entry: Dictionary) -> void:
	entry["group"] = entry.get("group", _group)
	_group += 1
	var at := Vector2(float(entry.x), float(entry.z))
	entry["hex"] = w.tile_at(at)
	_items.append(entry)
	if not bool(entry.get("outer", false)):
		wb._record_visual_occupancy(at, float(entry.get("r", 0.1)))

## Draws a mesh centred (by its bounds) on `at`, scaled to fit radius `fit` but never above `nominal`. Returns the scale.
func _put(mesh_name: String, at: Vector2, yaw: float, fit: float, nominal: float, tint: Color, meta: Dictionary, y: float = INF) -> float:
	var f := _foot(mesh_name)
	var s := minf(nominal, fit / maxf(float(f[0]), 0.001))
	var o: Vector3 = M7WorldBuilder.rot_y(yaw) * Vector3((f[1] as Vector2).x, 0.0, (f[1] as Vector2).y) * s
	var p := at - Vector2(o.x, o.z)
	var ground := w.height_at(at) if y == INF else y
	wb.props.add(mesh_name, wb._xf(p, ground, yaw, s), tint)
	var e := meta.duplicate()
	e["mesh"] = mesh_name
	e["x"] = at.x
	e["z"] = at.y
	e["r"] = float(f[0]) * s
	_record(e)
	return s

func _meta(kind: String, hi: int, slot: String, chain: String = "", tier: String = "") -> Dictionary:
	return {"kind": kind, "quarter": _quarter_of(hi) if hi >= 0 else "outer", "slot": slot, "chain": chain, "tier": tier,
		"slot_hex": int(_hex[hi].tile) if hi >= 0 else -1}

func _small(prefs: Array, kind: String, mesh_name: String, nominal: float = CORE_VS, chain: String = "", tier: String = "") -> Variant:
	var at := _take_small(prefs)
	if at.is_empty():
		_dropped.append(kind)
		return null
	var p := _slot_pos(at[0], at[1])
	_put(mesh_name, p, _face(p), SMALL_R, nominal, _tint(_items.size()), _meta(kind, at[0], "s%d" % at[1], chain, tier))
	return [at[0], at[1], p]

func _half(prefs: Array, kind: String, mesh_name: String, nominal: float, chain: String = "", tier: String = "") -> Variant:
	var at := _take_half(prefs)
	if at.is_empty():
		return null
	var p: Vector2 = (_hex[at[0]].c as Vector2) + _dir(at[1]).rotated(deg_to_rad(30.0)) * HALF_OFF
	var s := _put(mesh_name, p, _face(p), HALF_R, nominal, _tint(_items.size()),
		_meta(kind, at[0], "half:s%d+s%d" % [at[1] + 1, posmod(at[1] + 1, 6) + 1], chain, tier))
	return [at[0], p, s]

func _whole(prefs: Array, kind: String, mesh_name: String, nominal: float, chain: String = "", tier: String = "", fixed_scale: float = 0.0) -> Variant:
	var hi := _take_whole(prefs)
	var district_q := int(prefs[0]) if not prefs.is_empty() else -1
	var shown_scale := fixed_scale
	if shown_scale <= 0.0 and _unhoused_quarters.has(district_q):
		shown_scale = nominal * 0.7
	if hi < 0:
		# overflow (§4.4): half size on three petals of the Workshops hex, else listed only
		var half = _take_half([Q.WORKS])
		if half.is_empty():
			_dropped.append(kind)
			return null
		var hp: Vector2 = (_hex[half[0]].c as Vector2) + _dir(half[1]).rotated(deg_to_rad(30.0)) * HALF_OFF
		var hs := _put(mesh_name, hp, _face(hp), HALF_R, nominal * (0.7 if _unhoused_quarters.has(district_q) else 1.0), _tint(_items.size()),
			_meta(kind, half[0], "half:s%d+s%d" % [half[1] + 1, posmod(half[1] + 1, 6) + 1], chain, tier))
		return [half[0], hp, hs]
	var p: Vector2 = _hex[hi].c
	var fit := WHOLE_R
	if hi == _qhex[Q.FRONT]:
		p -= _dir(_front) * (WHOLE_R - FRONT_WHOLE_R)
		fit = FRONT_WHOLE_R
	var s: float
	if shown_scale > 0.0:
		var f := _foot(mesh_name)
		s = shown_scale
		var yaw := _face(p)
		var o: Vector3 = M7WorldBuilder.rot_y(yaw) * Vector3((f[1] as Vector2).x, 0.0, (f[1] as Vector2).y) * s
		wb.props.add(mesh_name, wb._xf(p - Vector2(o.x, o.z), w.height_at(p), yaw, s), _tint(_items.size()))
		var e := _meta(kind, hi, "whole", chain, tier)
		e["mesh"] = mesh_name
		e["x"] = p.x
		e["z"] = p.y
		e["r"] = float(f[0]) * s
		_record(e)
	else:
		s = _put(mesh_name, p, _face(p), fit, nominal, _tint(_items.size()), _meta(kind, hi, "whole", chain, tier))
	return [hi, p, s]

## A few meshes in one sub-slot (a cluster of houses, a rack yard): one dump entry with the slot's footprint.
func _cluster(prefs: Array, kind: String, meshes: Array, chain: String = "", tier: String = "") -> bool:
	var at := _take_small(prefs)
	if at.is_empty():
		_dropped.append(kind)
		return false
	var p := _slot_pos(at[0], at[1])
	var n := meshes.size()
	var face := _face(p)
	var across := Vector2(cos(face), -sin(face))
	var sub_r := SMALL_R if n == 1 else (SMALL_R * 0.5 if n == 2 else SMALL_R * 0.54)
	for i in n:
		var q := p
		var mesh_name: String = meshes[i]
		var f := _foot(mesh_name)
		var s := minf(CORE_VS, sub_r / maxf(float(f[0]), 0.001))
		if n == 2:
			# a pair side by side (a terrace of two): sized by their boxes so both stay inside the sub-slot
			var hb: Vector2 = f[2]
			s = minf(CORE_VS, SMALL_R / Vector2(2.0 * hb.x, hb.y).length())
			q = p + across * (hb.x * s * (1 if i == 0 else -1))
		elif n >= 3:
			q = p + Vector2(cos(face + TAU * i / n), sin(face + TAU * i / n)) * (SMALL_R * 0.46)
		var o: Vector3 = M7WorldBuilder.rot_y(face) * Vector3((f[1] as Vector2).x, 0.0, (f[1] as Vector2).y) * s
		wb.props.add(mesh_name, wb._xf(q - Vector2(o.x, o.z), w.height_at(q), face, s), _tint(_items.size() + i))
	var e := _meta(kind, at[0], "s%d" % at[1], chain, tier)
	e["mesh"] = ",".join(meshes)
	e["count"] = n
	e["x"] = p.x
	e["z"] = p.y
	e["r"] = SMALL_R
	_record(e)
	return true

# ------------------------------------------------------------------ one settlement

## Distance from a circle to an oriented box [cx, cz, half x, half z, angle of the box's x axis] (negative when they overlap).
static func _box_gap(p: Vector2, r: float, b: Array) -> float:
	var d := p - Vector2(b[0], b[1])
	var u := Vector2(cos(float(b[4])), sin(float(b[4])))
	var lx := absf(d.dot(u)) - float(b[2])
	var lz := absf(d.dot(Vector2(-u.y, u.x))) - float(b[3])
	return Vector2(maxf(lx, 0.0), maxf(lz, 0.0)).length() - r

## The harbor quay's footprint where world_builder._harbor will put it (on the Front's water edge), as a box.
func _harbor_box() -> Array:
	var dir := _dir(_front)
	var at := _c0 + dir * 0.5
	var yaw := atan2(-dir.x, -dir.y)
	var f := _foot("harbor")
	var o: Vector3 = M7WorldBuilder.rot_y(yaw) * Vector3(f[1].x, 0.0, f[1].y) * 1.15
	return [at.x + o.x, at.y + o.z, float(f[2].x) * 1.15, float(f[2].y) * 1.15, -yaw]

## Lays out one live settlement. `c` is where the caller would put the hearth; the core puts it at the tile centre or, off a river, toward
## the Front (the Heart's way in), and keeps the petals beside it clear.
func settlement(si: int, s: Dictionary, c: Vector2, flagship: Dictionary) -> void:
	var K := M7WorldData.Kind
	_si = si
	_t = int(s.tile)
	_civ = int(s.civ)
	_c0 = w.tile_center(_t)
	_c = _c0
	_h0 = w.heights[_t]
	_seed = float(_t % 997) / 997.0
	_cc = M7MeshBuilder.lin(M7WorldData.civ_color(_civ), 0.5)
	_stage = stage_of(s)
	_items = []
	_dropped = []
	var st: Array = s.structures
	_front = int(s.get("coreFront", 255))
	if _front < 0 or _front >= 6:
		_front = _t % 6
	var front_tile := w.neighbour(_t, _front)
	_water_front = front_tile >= 0 and w.is_water(front_tile)
	_ring2 = []
	_unhoused_quarters.clear()
	_gate_i = -2
	for row in s.get("coreOuter", []):
		_ring2.append([int(row[0]), int(row[1]), int(row[2]), -1])
	_build_hexes()
	_assign_quarters()
	# s0 of the Heart holds the hearth and banner; the Heart petal toward the Front is the way in (gate passage or quay); so is the Front
	# hex's outermost petal under the gate
	_hex[0].slots[0] = 1
	_hex[0].slots[_front + 1] = 2
	if _qhex_all[Q.FRONT] >= 0 and _stage >= 1:
		var fh: int = _qhex_all[Q.FRONT]
		if _hex[fh].slots[_front + 1] == 0:
			_hex[fh].slots[_front + 1] = 2
	if wb._river_dist(_c0) < 0.14:
		for off in [0.15, 0.22]:
			var found := false
			for k in [0, 1, -1, 2, -2, 3]:
				var q := _c0 + _dir(_front + int(k)) * float(off)
				if wb._river_dist(q) >= 0.16 and w.is_land(w.tile_at(q)):
					_c = q
					found = true
					break
			if found:
				break
		if wb.hearths.size() > 0:
			wb.hearths[wb.hearths.size() - 1] = Vector3(_c.x, _h0, _c.y)
		for k in range(1, 7):
			if _slot_pos(0, k).distance_to(_c) < SMALL_R + 0.12 and _hex[0].slots[k] == 0:
				_hex[0].slots[k] = 1
	# a harbor's quay reaches over the shore: the sub-slots under it stay clear
	if _water_front and w.iron_works(s, "harbor") > 0:
		var hb := _harbor_box()
		for hi in _hex.size():
			for k in 7:
				if _hex[hi].slots[k] == 0 and _box_gap(_slot_pos(hi, k), SMALL_R, hb) < 0.01:
					_hex[hi].slots[k] = 3
	if int(st[K.STONE_WALLS]) > 0 or wb._built(st, K.PALISADE) > 0 or int(st[K.THORN_BARRIER]) > 0:
		_reserve_gate()
	var name := String(s.get("coreGallery", ""))
	if name != "":
		gallery[name] = Vector3(_c0.x, _h0, _c0.y)

	# hearth and banner (s0); the banner's footprint is its pole
	if int(st[K.HEARTH]) > 0:
		wb.props.add("hearth", wb._xf(_c, _h0, _seed * TAU, CORE_VS), Color(1, 1, 1, _seed))
		if wb._fire_lit(s):
			wb.props.add("flame", wb._xf(_c, _h0 + 0.004, _seed * TAU, CORE_VS), Color(1, 1, 1, _seed))
	var e := _meta("hearth", 0, "s0")
	e.merge({"mesh": "hearth", "x": _c.x, "z": _c.y, "r": float(_foot("hearth")[0]) * CORE_VS})
	var hearth_group := _group
	_record(e)
	var banner_at := _c + _dir(_front + 3).rotated(0.5) * 0.1
	wb.props.add("banner%d" % _civ, wb._xf(banner_at, _h0, -0.4, 1.0), Color(_cc.r, _cc.g, _cc.b, _seed))
	_record({"kind": "banner", "mesh": "banner%d" % _civ, "quarter": "heart", "slot": "s0", "chain": "", "tier": "", "slot_hex": _t,
		"x": banner_at.x, "z": banner_at.y, "r": 0.02, "group": hearth_group})

	var decor: Array = []
	for k in wb._village_decor(s.get("decor", PackedInt32Array()), _civ, si, flagship):
		var ki := int(k)
		if ki >= 0 and ki < M7MeshKit.DECOR_NAMES.size():
			decor.append(String(M7MeshKit.DECOR_NAMES[ki]))
	var dropped_dup := []
	for d in DUPLICATES:
		if decor.has(d):
			dropped_dup.append("decor_" + d)

	# ---- whole-hex buildings, in priority order: Wonder, castle, ziggurat, temple, civic works (§4.4)
	var big := [Q.SACRED, Q.HOMES_B, Q.FRONT, Q.WATCH]
	for wid in [["great_dome", "dome", "great_dome", 1.35], ["great_library", "library", "great_library", 1.2]]:
		if w.iron_works(s, String(wid[0])) > 0:
			var wonder_q := Q.WONDER_A if String(wid[0]) == "great_dome" else Q.WONDER_C
			var r = _whole([wonder_q] + big, String(wid[1]), String(wid[2]), float(wid[3]))
			if r != null:
				wb.civic_sites.append({"kind": String(wid[1]), "p": r[1], "y": w.height_at(r[1]), "yaw": _face(r[1]), "civ": _civ, "seed": _seed, "tile": _t})
	var build_k := int(s.get("ironBuildKind", -1))
	var build_id := String(w.iron_work_ids[build_k]) if build_k >= 0 and build_k < w.iron_work_ids.size() else ""
	var build_pm := int(s.get("ironBuildPermille", 0))
	var site_mesh := "build_site_%d" % (1 + mini(2, build_pm * 3 / 1000))
	if build_pm > 0 and build_id.begins_with("great_") and build_id != "great_lighthouse":
		var build_q := Q.WONDER_A if build_id == "great_dome" else Q.WONDER_C
		_whole([build_q] + big, "wonder_site", site_mesh, 2.1)
	var cstage: int = wb.castle_stage(s)
	if cstage > 0:
		var r = _whole([Q.CITADEL, Q.WATCH, Q.HOMES_B, Q.FRONT, Q.SACRED], "castle", "castle_%d" % cstage, 1.25)
		if r != null:
			var cp: Vector2 = r[1]
			var cyaw := _face(cp)
			wb.castle_sites.append({"p": cp, "y": w.height_at(cp), "yaw": cyaw, "stage": cstage, "civ": _civ, "tile": _t})
			var guards := mini(int(s.get("soldiers", 0)), 6) if cstage == 4 else 0
			for gi in guards:
				var ang := TAU * gi / 6.0 + 0.4
				var gp: Vector3 = M7WorldBuilder.rot_y(cyaw) * (Vector3(cos(ang), 0.0, sin(ang)) * 0.15 * float(r[2]))
				var gb := Basis(Vector3.UP, cyaw + ang + PI * 0.5) * Basis.from_scale(Vector3.ONE * 1.0)
				wb.props.add("person_soldier", Transform3D(gb, Vector3(cp.x + gp.x, w.height_at(cp) + 0.02 * float(r[2]), cp.y + gp.z)),
					Color(float(_civ) + 0.5 * 0.98, 0.0, 0.0, 0.0))
	var zs: int = wb.zig_stage(s)
	if zs > 0:
		# the ziggurat is too big for three Heart petals (§4.3 asks for that): it takes a whole hex like the other landmarks
		var r = _whole([Q.WONDER_B] + big, "ziggurat", M7MeshKit.ZIG_MESHES[zs - 1], 1.3, "", "")
		if r != null:
			var zp: Vector2 = r[1]
			var zyaw := _face(zp)
			if zs == 4:
				var o: Vector3 = M7WorldBuilder.rot_y(zyaw) * (Vector3(-0.056, 0.314, -0.09) * float(r[2]))
				wb.props.add("banner%d" % _civ, wb._xf(zp + Vector2(o.x, o.z), w.height_at(zp) + o.y, -0.4, 1.0), Color(_cc.r, _cc.g, _cc.b, _seed))
			wb.ziggurat_sites.append({"p": zp, "y": w.height_at(zp), "yaw": zyaw, "stage": zs, "civ": _civ, "seed": _seed, "tile": _t, "scale": float(r[2])})
	# the sacred chain (§4.5): totem, shrine, temple, temple with colonnade; only the best is drawn
	var sacred_tier := ""
	if int(s.get("temples", 0)) > 0:
		sacred_tier = "temple_colonnade" if w.has_look(_civ, M7WorldData.LOOK_COLONNADES) or w.iron_works(s, "temple_tier2") > 0 else "temple"
	elif decor.has("temple"):
		sacred_tier = "stepped_temple"
	elif wb._built(st, K.SHRINE) > 0:
		sacred_tier = "shrine"
	elif wb._built(st, K.TOTEM) > 0:
		sacred_tier = "totem"
	if sacred_tier == "temple" or sacred_tier == "temple_colonnade":
		var r = _whole(big, "temple", "temple_tier2" if sacred_tier == "temple_colonnade" else "temple", 1.3, "sacred", sacred_tier)
		if r != null:
			wb.civic_sites.append({"kind": "temple", "p": r[1], "y": w.height_at(r[1]), "yaw": _face(r[1]), "civ": _civ, "seed": _seed, "tile": _t})
	for cw in [["academy", "academy", "academy", Q.LEARNING], ["theatre", "theatre_arena", "amphitheatre", Q.CIVIC], ["bathhouse", "bathhouse", "bathhouse", Q.HEALTH]]:
		if w.iron_works(s, String(cw[1])) > 0:
			var r = _whole([int(cw[3])] + big, String(cw[0]), String(cw[2]), 1.3)
			if r != null:
				wb.civic_sites.append({"kind": String(cw[0]), "p": r[1], "y": w.height_at(r[1]), "yaw": _face(r[1]), "civ": _civ, "seed": _seed, "tile": _t})
				if cw[0] == "bathhouse":
					var off: Vector3 = M7WorldBuilder.rot_y(_face(r[1])) * (Vector3(-0.08, 0.205, -0.05) * float(r[2]))
					wb.chimney_smokes.append({"pos": Vector3(r[1].x + off.x, w.height_at(r[1]) + off.y, r[1].y + off.z), "seed": _seed})
	if build_pm > 0 and build_id in ["academy", "theatre_arena", "bathhouse"]:
		_whole(big, "site", site_mesh, 1.3)

	# ---- the Heart landmark: the council chain (council ring, chief's hall; the palace has no mesh yet)
	if decor.has("chiefhall"):
		if _stage == 0:
			# A Band's thorn perimeter encloses only the Heart; keep the hall within one petal instead of crossing that wall.
			_small([Q.HEART], "chiefhall", "decor_chiefhall", CORE_VS, "council", "chiefhall")
		else:
			var hp := [posmod(_front + 2, 6) + 1, posmod(_front + 3, 6) + 1, posmod(_front + 4, 6) + 1]
			if _hex[0].slots[hp[0]] == 0 and _hex[0].slots[hp[1]] == 0 and _hex[0].slots[hp[2]] == 0:
				for k in hp:
					_hex[0].slots[k] = 1
				var lp := _c0 + _dir(_front + 3) * HEART3_OFF
				_put("decor_chiefhall", lp, _face(lp) if lp.distance_to(_c) > 0.01 else 0.0, HEART3_R, CORE_VS, _tint(91), _meta("chiefhall", 0, "heart3", "council", "chiefhall"))
			elif _half([Q.HOMES_B, Q.SACRED, Q.WORKS, Q.HOMES_A], "chiefhall", "decor_chiefhall", CORE_VS, "council", "chiefhall") == null:
				_dropped.append("chiefhall")
	elif decor.has("council"):
		_small([Q.HEART], "council", "decor_council", CORE_VS, "council", "council_ring")

	# ---- half-hex yards: food store, pottery, metal, drill ground, crane, press (§4.6)
	var works_q := [Q.WORKS, Q.HOMES_B, Q.WATCH, Q.FRONT, Q.SACRED, Q.HOMES_A, Q.HEART]
	var yard_q := [Q.WORKS, Q.WATCH, Q.FRONT, Q.SACRED]     # yards never take a Homes quarter's s0 (a house per Homes quarter comes first)
	var pits := wb._built(st, K.STORAGE_PIT)
	var granaries := wb._built(st, K.GRANARY)
	if granaries > 0:
		var g = _half(yard_q, "granary", "granary", 1.5 if granaries >= 3 else 1.3, "food_store", "granary")
		if g == null:
			_small(works_q, "granary", "granary", CORE_VS, "food_store", "granary")
	var pottery_tier := ""
	if wb._built(st, K.POTTERS_WORKSHOP) > 0:
		pottery_tier = "kiln_workshop"
		var y = _half(yard_q, "potters_workshop", "potters_workshop", CORE_VS, "pottery", pottery_tier)
		if y == null:
			_small(works_q, "potters_workshop", "potters_workshop", CORE_VS, "pottery", pottery_tier)
	var forge := int(s.get("forge", 0))
	if forge > 0:
		var fy = _half(yard_q, "forge_yard", "forge_yard_hot" if forge >= 2 else "forge_yard", 1.2, "metal", "forge_yard")
		if fy == null:
			fy = _small(works_q, "forge_yard", "forge_yard_hot" if forge >= 2 else "forge_yard", 1.2, "metal", "forge_yard")
		if fy != null and forge >= 2:
			var fp: Vector2 = fy[1] if fy.size() == 3 and fy[1] is Vector2 else fy[2]
			var fs: float = _items[-1].r / maxf(float(_foot("forge_yard_hot")[0]), 0.001)
			var rot := M7WorldBuilder.rot_y(_face(fp))
			var chimney: Vector3 = rot * (Vector3(0, 0.185, -0.05) * fs)
			var mouth: Vector3 = rot * (Vector3(0, 0.04, 0.014) * fs)
			var fyh := w.height_at(fp)
			wb.forge_sites.append({"chimney": Vector3(fp.x + chimney.x, fyh + chimney.y, fp.y + chimney.z),
				"mouth": Vector3(fp.x + mouth.x, fyh + mouth.y, fp.y + mouth.z), "seed": _seed})
	var soldiers := mini(int(s.get("soldiers", 0)), 6)
	if soldiers > 0:
		var dg := _take_half([Q.WATCH, Q.FRONT, Q.HOMES_B])
		if not dg.is_empty():
			var q: Vector2 = (_hex[dg[0]].c as Vector2) + _dir(dg[1]).rotated(deg_to_rad(30.0)) * HALF_OFF
			var face := _face(q)
			var across := Vector2(cos(face), -sin(face))
			var fwd := (_c - q).normalized()
			for i in soldiers:
				var p := q + across * ((i % 3 - 1) * 0.085) - fwd * ((i / 3) * 0.085 - 0.04)
				wb.props.add("person_soldier", wb._xf(p, w.height_at(p), face, 1.0), Color(float(_civ) + 0.5 * 0.98, 0.0, 0.0, 0.0))
			var de := _meta("drill_ground", dg[0], "half:s%d+s%d" % [dg[1] + 1, posmod(dg[1] + 1, 6) + 1])
			de.merge({"mesh": "person_soldier", "count": soldiers, "x": q.x, "z": q.y, "r": 0.17})
			_record(de)
		else:
			_dropped.append("drill_ground")
	for cw in [["crane", "crane"], ["press", "press"]]:
		if w.iron_works(s, String(cw[0])) > 0:
			var r = _half(yard_q, String(cw[0]), String(cw[1]), 1.3)
			if r != null:
				wb.civic_sites.append({"kind": String(cw[0]), "p": r[1], "y": w.height_at(r[1]), "yaw": _face(r[1]), "civ": _civ, "seed": _seed, "tile": _t})
			else:
				_small(works_q, String(cw[0]), String(cw[1]), 1.3)
	if build_pm > 0 and build_id in ["crane", "press"]:
		if _half(yard_q, "site", site_mesh, 1.3) == null:
			_dropped.append("site")
	if sacred_tier == "stepped_temple":
		if _half([Q.SACRED, Q.HOMES_B, Q.WORKS, Q.HEART], "temple", "decor_temple", CORE_VS, "sacred", sacred_tier) == null:
			_small([Q.SACRED, Q.HOMES_B, Q.WORKS, Q.HEART], "temple", "decor_temple", CORE_VS, "sacred", sacred_tier)

	# ---- houses (§4.3): one per sub-slot of the Homes quarters (and up to three Heart petals in a Village, the Heart in a Band), then
	# clusters of two in a Town and three in a City; the rest are implied by the clusters
	_houses(s, st, decor)

	# ---- small items, highest priority first (§4.4)
	if granaries == 0 and pits > 0:
		_cluster(works_q, "storage_pits", ["storagepit", "storagepit"] if pits >= 2 else ["storagepit"], "food_store", "pits")
	if wb._built(st, K.KILN) > 0:
		_small(works_q, "kiln", "kiln", CORE_VS, "pottery" if pottery_tier == "" else "", "kiln" if pottery_tier == "" else "")
	_watch(s, st, decor)
	if sacred_tier == "shrine":
		_small([Q.SACRED, Q.HOMES_B, Q.WORKS, Q.HEART], "shrine", "shrine", CORE_VS, "sacred", "shrine")
	elif sacred_tier == "totem":
		_small([Q.SACRED, Q.HEART, Q.WORKS], "totem", "totem", CORE_VS, "sacred", "totem")
	var racks := wb._built(st, K.DRYING_RACK)
	if racks > 0:
		_cluster(works_q + [Q.HEART], "drying_racks", ["dryingrack", "dryingrack"] if racks >= 4 else ["dryingrack"])
	var water_mill := w.iron_works(s, "water_mill") > 0
	if wb._built(st, K.QUERN) > 0:
		_small(works_q + [Q.HEART], "quern", "quern", CORE_VS, "" if water_mill else "grinding", "" if water_mill else "quern")
	if forge <= 0 and decor.has("furnace"):
		_small(works_q, "furnace", "decor_furnace", CORE_VS, "metal", "furnace")
	if w.iron_works(s, "windlass_well") > 0:
		_small([Q.FRONT, Q.WORKS, Q.HOMES_B, Q.HEART], "well", "windlass_well", 1.3)
	var aqueduct_at = null
	if w.iron_works(s, "aqueduct") > 0:
		aqueduct_at = _small([Q.FRONT, Q.WORKS, Q.HOMES_B, Q.WATCH], "aqueduct", "aqueduct_end", 1.3)
	if build_pm > 0 and build_id in ["windlass_well", "aqueduct"]:
		_small([Q.FRONT, Q.WORKS, Q.HOMES_B], "site", site_mesh, 0.7)
	if _civ >= 0 and _civ < w.iron_looks.size():
		for sp in [[M7WorldData.IRON_BARRELS, "barrel_stack"], [M7WorldData.IRON_PLANKS, "plank_stack"], [M7WorldData.IRON_GLASS, "glass_shelf"]]:
			if w.has_iron_look(_civ, int(sp[0])):
				_small([Q.WORKS, Q.HOMES_B, Q.FRONT], String(sp[1]).trim_suffix("_stack").trim_suffix("_shelf"), String(sp[1]), 1.35)
	_flags(s, si, flagship)
	if decor.has("skypole"):
		_small([Q.HEART, Q.SACRED], "skypole", "decor_skypole")
	if decor.has("spearrack"):
		_small([Q.WATCH, Q.FRONT, Q.HOMES_B], "spearrack", "decor_spearrack")
	if decor.has("chariot"):
		_small([Q.WATCH, Q.FRONT, Q.HOMES_B], "chariot", "decor_chariot")
	if decor.has("cairn"):
		_small([Q.FRONT, Q.SACRED], "cairn", "decor_cairn")
	# ---- dressing beside its host (§4.6): one each, dropped first when there is no room
	for dn in ["grave", "hideframe", "loom", "woolbales", "pots", "bricks", "baskets", "woodwork", "sledge"]:
		if decor.has(dn):
			_small(DRESSING[dn], dn, "decor_" + dn, CORE_VS * 0.9, "", "dressing")

	_gardens()
	var walls := int(st[K.STONE_WALLS])
	var watch_tower := walls > 0 and (int(st[K.LOOKOUT]) > 0 or wb._built(st, K.SIGNAL_POST) > 0)
	_perimeter(walls, wb._built(st, K.PALISADE) > 0, int(st[K.THORN_BARRIER]) > 0, w.iron_works(s, "gatehouse") > 0, watch_tower,
		wb._built(st, K.SIGNAL_POST) > 0)
	_waterfront(s, st)
	_outer(s, si, st, decor, aqueduct_at)
	for d in dropped_dup:
		_dropped.append(d)
	wb.villages.append(Vector3(_c.x, _c.y, VILLAGE_GROUND))
	# Claimed ring-2 hexes are village ground too: one trampled-earth patch per hex (the terrain shader's village list).
	for hi in range(7, _hex.size()):
		wb.villages.append(Vector3((_hex[hi].c as Vector2).x, (_hex[hi].c as Vector2).y, RING2_GROUND))
	var core_tiles := []
	for hx in _hex:
		core_tiles.append(int(hx.tile))
	var quarters := {}
	for q in QN:
		quarters[Q_NAMES[q]] = int(_hex[_qhex_all[q]].tile) if _qhex_all[q] >= 0 else -1
	var active := []
	for q in QN:
		if _qhex[q] >= 0:
			active.append(Q_NAMES[q])
	var sim_outer := []
	var rendered_outer_tiles := []
	var rendered_outer_districts := []
	var unhoused_districts := []
	for r in _ring2:
		sim_outer.append([int(r[0]), int(r[1]), int(r[2])])
		if int(r[0]) >= 0:
			rendered_outer_tiles.append(int(r[0]))
			rendered_outer_districts.append(_district_name(int(r[1])))
		else:
			unhoused_districts.append(_district_name(int(r[1])))
	_rows.append({"settlement": si, "tile": _t, "civ": _civ, "stage": STAGES[_stage], "gallery": name, "front": _front, "water_front": _water_front,
		"population": int(s.population), "core_tiles": core_tiles, "sim_outer": sim_outer, "rendered_outer_tiles": rendered_outer_tiles,
		"rendered_outer_districts": rendered_outer_districts, "unhoused_districts": unhoused_districts, "core_expected_items": _expected_whole_items(s), "quarters": quarters,
		"active_quarters": active, "items": _items, "dropped": _dropped})

func _houses(s: Dictionary, st: Array, decor: Array) -> void:
	var tiled: bool = _civ >= 0 and w.has_look(_civ, M7WorldData.LOOK_TILES)
	var homes_free := _free_small([Q.HOMES_A, Q.HOMES_B, Q.HOMES_C, Q.HOMES_D])
	# the Heart lends a Band all its petals but two for its stores, a Village up to three
	var heart_free := 0
	for k in range(1, 7):
		if _hex[0].slots[k] == 0:
			heart_free += 1
	var heart_quota := 0
	if _stage == 0:
		heart_quota = maxi(1, heart_free - mini(2, int(wb._built(st, M7WorldData.Kind.STORAGE_PIT) > 0) + int(wb._built(st, M7WorldData.Kind.DRYING_RACK) > 0)))
	elif _stage == 1:
		heart_quota = mini(3, maxi(0, heart_free - 1))
	var free := _free_small([Q.HOMES_A, Q.HOMES_B, Q.HOMES_C, Q.HOMES_D]) + heart_quota
	var per := 3 if _stage == 3 else (2 if _stage == 2 else 1)
	var houses: Array = wb._dwellings(st, int(s.get("halls", 0)), int(s.population), free * per)
	var big := []
	var small := []
	for h in houses:
		if h == "hall" or h == "longhouse":
			big.append(h)
		else:
			small.append(h)
	var halves := 0
	for h in big:
		var mesh_name: String = h + "_tile" if tiled and h in M7WorldBuilder.TILED_HOUSES else h
		# a hall or longhouse gets its half-hex (three sub-slots) only while every house still fits one to a sub-slot
		if halves < 2 and houses.size() + 2 * (halves + 1) <= homes_free and _half([Q.HOMES_A, Q.HOMES_B, Q.HOMES_C, Q.HOMES_D], "house_" + h, mesh_name, CORE_VS, "shelter", "houses") != null:
			halves += 1
		else:
			small.push_front(h)
	per = 1
	if small.size() > free:
		per = 2 if _stage == 2 else (3 if _stage == 3 else 1)
	var heart_used := 0
	var i := 0
	while i < small.size():
		var n := mini(per, small.size() - i)
		var group := []
		for j in n:
			var h: String = small[i + j]
			group.append(h + "_tile" if tiled and h in M7WorldBuilder.TILED_HOUSES else h)
		var prefs := [Q.HOMES_A, Q.HOMES_B, Q.HOMES_C, Q.HOMES_D]
		if heart_used < heart_quota:
			prefs.append(Q.HEART)
		var before: Array = (_hex[0].slots as Array).duplicate()
		if not _cluster(prefs, "house_" + String(small[i]), group, "shelter", "houses"):
			_dropped.pop_back()
			_dropped.append("houses_implied_%d" % (small.size() - i))
			break
		if _hex[0].slots != before:
			heart_used += 1
		i += n

## The watch chain (§4.5): lookout, lookout with a signal brazier, a wall tower once walled (drawn with the perimeter).
func _watch(s: Dictionary, st: Array, decor: Array) -> void:
	var K := M7WorldData.Kind
	var lookout := int(st[K.LOOKOUT]) > 0
	var post := wb._built(st, K.SIGNAL_POST) > 0
	if int(st[K.STONE_WALLS]) > 0 and (lookout or post):
		return
	var prefs := [Q.WATCH, Q.FRONT, Q.WORKS, Q.SACRED, Q.HOMES_B, Q.HEART]
	if lookout:
		var r = _small(prefs, "lookout", "lookout", CORE_VS, "watch", "lookout_brazier" if post else "lookout")
		if r != null:
			var p: Vector2 = r[2]
			var y := w.height_at(p)
			var ls: float = float(_items[-1].r) / maxf(float(_foot("lookout")[0]), 0.001)
			wb.props.add("banner_small%d" % _civ, wb._xf(p, y + 0.285 * ls, -0.4, ls), Color(_cc.r, _cc.g, _cc.b, _seed))
			if post:
				wb.props.add("flame", wb._xf(p, y + 0.33 * ls, 0.0, 0.7), Color(1, 1, 1, _seed))
	elif post:
		_small(prefs, "signal_post", "signal_post", CORE_VS, "watch", "signal_post")

## Flags on the meeting ground (§4.6): pacts, faith standards, a province seat's standard and the feudal banner, on Heart petals.
func _flags(s: Dictionary, si: int, flagship: Dictionary) -> void:
	var prefs := [Q.HEART, Q.FRONT, Q.WATCH]
	if int(flagship.get(_civ, -1)) == si:
		var overlord := w.overlord_of(_civ)
		if overlord >= 0:
			var t := M7MeshBuilder.lin(M7WorldData.civ_color(overlord), 0.5)
			_flag(prefs, "vassal_banner", Color(t.r, t.g, t.b, _seed), 1)
		if w.in_league(_civ):
			_flag(prefs, "league_standard", Color(_cc.r, _cc.g, _cc.b, _seed), 1)
	var permille := 0
	var faith_civ := -1
	if int(s.get("faithForeign", 0)) > 0:
		permille = int(s.faithForeign)
		faith_civ = int(s.get("faithCiv", -1))
	elif int(s.get("faithOur", 0)) > 0:
		permille = int(s.faithOur)
		faith_civ = w.viewer_civ
	if faith_civ >= 0 and permille >= 200:
		var ft := M7MeshBuilder.lin(M7WorldData.civ_color(faith_civ), 0.5)
		_flag(prefs, "faith_standard", Color(ft.r, ft.g, ft.b, _seed), 1 + (1 if permille >= 450 else 0) + (1 if permille >= 700 else 0))
	if int(s.get("provinceSeat", -1)) >= 0 and int(s.get("provinceSeat", -1)) == si:
		_flag(prefs, "province_standard", Color(_cc.r, _cc.g, _cc.b, _seed), 1)
	if int(s.get("contract", 0)) > 0:
		_flag(prefs, "feudal_banner", Color(_cc.r, _cc.g, _cc.b, _seed), 1)

func _flag(prefs: Array, mesh_name: String, tint: Color, count: int) -> void:
	var at := _take_small(prefs)
	if at.is_empty():
		_dropped.append(mesh_name)
		return
	var p := _slot_pos(at[0], at[1])
	var face := _face(p)
	var across := Vector2(cos(face), -sin(face))
	for i in count:
		var q := p + across * ((i - (count - 1) * 0.5) * 0.08)
		wb.props.add(mesh_name, wb._xf(q, w.height_at(q), face, 1.1), tint)
	var e := _meta(mesh_name, at[0], "s%d" % at[1])
	e.merge({"mesh": mesh_name, "count": count, "x": p.x, "z": p.y, "r": 0.04 + 0.04 * (count - 1)})
	_record(e)

# ------------------------------------------------------------------ garden cues for plots the sim keeps on core hexes

func _clear_of_items(p: Vector2, r: float) -> bool:
	for it in _items:
		if it.has("box"):
			var b: Array = it.box
			if p.distance_to(Vector2(b[0], b[1])) < r + Vector2(b[2], b[3]).length():
				return false
		elif p.distance_to(Vector2(float(it.x), float(it.z))) < r + float(it.r):
			return false
	return true

func _gardens() -> void:
	for hi in _hex.size():
		var tile := int(_hex[hi].tile)
		if tile < 0 or not w.is_land(tile) or w.plot_kind[tile] < 0:
			continue
		if _sim_claimed_tiles.has(tile):
			_dropped.append("hidden_claimed_plot_%d" % tile)
			continue
		if hi >= 7:
			# a reserved ring-2 hex: the staged rule releases its plot at the next season start (31a §3.4); until the sim does, it is not drawn
			garden_cues[tile] = [Vector2.ZERO, 0.0]
			_dropped.append("released_plot_%d" % tile)
			continue
		var hx: Dictionary = _hex[hi]
		var empty := hi != 0
		for k in 7:
			if hx.slots[k] == 1:
				empty = false
		var spot := Vector2.INF
		var r := 0.0
		var slot := ""
		if empty and hx.slots[0] == 0 and _clear_of_items(hx.c, GARDEN_OPEN_R):
			spot = hx.c
			r = GARDEN_OPEN_R
			slot = "s0"
			for k in 7:
				if hx.slots[k] == 0:
					hx.slots[k] = 1
		if spot == Vector2.INF:
			for k in _petal_order(hi):
				if hx.slots[k] == 0 and _clear_of_items(_slot_pos(hi, k), GARDEN_PETAL_R):
					spot = _slot_pos(hi, k)
					r = GARDEN_PETAL_R
					slot = "s%d" % k
					hx.slots[k] = 1
					break
		if spot == Vector2.INF:
			for j in 6:
				var q: Vector2 = (hx.c as Vector2) + M7WorldData.corner(j).normalized() * 0.4
				if wb._river_dist(q) > GARDEN_GAP_R + 0.05 and _clear_of_items(q, GARDEN_GAP_R):
					spot = q
					r = GARDEN_GAP_R
					slot = "gap%d" % j
					break
		if spot == Vector2.INF:
			garden_cues[tile] = [Vector2.ZERO, 0.0]
			_dropped.append("garden_%d" % tile)
			continue
		garden_cues[tile] = [spot, r / FIELD_R]
		var e := _meta("garden", hi, slot)
		e.merge({"mesh": "field" if w.plot_kind[tile] == 0 else "orchard_tree", "x": spot.x, "z": spot.y, "r": r})
		_record(e)

# ------------------------------------------------------------------ the perimeter (one chain: thorn hedge, palisade, stone wall I to III)

func _inset_corner(hi: int, k: int, j: int, used: Dictionary, inset: float) -> Array:
	var hc: Vector2 = _hex[hi].c
	var other := k - 1 if j == k else k + 1
	var nb := w.neighbour(int(_hex[hi].tile), posmod(other, 6))
	var p := hc + M7WorldData.corner(posmod(j, 6))
	if nb >= 0 and used.has(nb):
		var rc := hc + _dir(k) * 1.0
		return [p + (p - rc).normalized() * ((0.5 - inset) / sin(PI / 3.0)), false]
	return [hc + M7WorldData.corner(posmod(j, 6)) * (inset / 0.5), true]

func _perimeter(walls: int, palisade: bool, thorn: bool, gatehouse: bool, watch_tower: bool, brazier: bool) -> void:
	var tier := ""
	if walls > 0:
		tier = "stone_wall_%d" % mini(walls, 3)
	elif palisade:
		tier = "palisade"
	elif thorn:
		tier = "thorn"
	if tier == "":
		return
	var inset := PERIM_THORN if tier == "thorn" else PERIM
	var outline := _outline_and_gate()
	var used: Dictionary = outline[0]
	var edges: Array = outline[1]
	var gate_i: int = outline[2]
	_draw_perimeter(tier, inset, used, edges, gate_i, walls, gatehouse, watch_tower, brazier)

## The outline of the hexes in use (edges whose far side is not in use) and the gate's edge: the land edge facing the Front most squarely
## (the Front hex's own outer edge on a land front). Returns [used tiles -> hex, edges [hex, direction, far tile], gate edge index].
func _outline_and_gate() -> Array:
	var used := {}
	for hi in _hex.size():
		if _hex[hi].use and int(_hex[hi].tile) >= 0:
			used[int(_hex[hi].tile)] = hi
	var edges := []
	for tile in used:
		var hi: int = used[tile]
		for k in 6:
			var nb := w.neighbour(tile, k)
			if nb >= 0 and used.has(nb):
				continue
			edges.append([hi, k, nb])
	if _gate_i != -2:
		return [used, edges, _gate_i]
	# the gate: on the land edge facing the Front most squarely (the Front hex's own outer edge on a land front)
	var best := -INF
	var gate_i := -1
	for i in edges.size():
		var e: Array = edges[i]
		if int(e[2]) < 0 or not w.is_land(int(e[2])):
			continue
		if _tile_has_whole_outer_claim(int(_hex[e[0]].tile)):
			continue
		var mid: Vector2 = (_hex[e[0]].c as Vector2) + _dir(e[1]) * 0.5
		if wb._river_dist(mid) < 0.14:
			continue
		var score := _dir(e[1]).dot(_dir(_front)) + (mid - _c0).normalized().dot(_dir(_front)) + (0.5 if int(e[0]) == _qhex_all[Q.FRONT] else 0.0)
		if score > best:
			best = score
			gate_i = i
	return [used, edges, gate_i]

func _tile_has_whole_outer_claim(tile: int) -> bool:
	for row in _ring2:
		if int(row[0]) != tile:
			continue
		var q := _quarter_for_district(int(row[1]))
		if q in [Q.CITADEL, Q.LEARNING, Q.CIVIC, Q.HEALTH, Q.WONDER_A, Q.WONDER_B, Q.WONDER_C]:
			return true
	return false

## Keeps the sub-slots under the gate (or gatehouse) and its approach clear.
func _reserve_gate() -> void:
	var outline := _outline_and_gate()
	var gate_i: int = outline[2]
	_gate_i = gate_i
	if gate_i < 0:
		return
	var e: Array = outline[1][gate_i]
	var mid: Vector2 = (_hex[e[0]].c as Vector2) + _dir(e[1]) * PERIM
	var box := [mid.x, mid.y, 0.2, 0.09, deg_to_rad(60.0 * int(e[1]) + 90.0)]
	for hi in _hex.size():
		for k in 7:
			if _hex[hi].slots[k] == 0 and _box_gap(_slot_pos(hi, k), SMALL_R, box) < 0.01:
				_hex[hi].slots[k] = 2

func _draw_perimeter(tier: String, inset: float, used: Dictionary, edges: Array, gate_i: int, walls: int, gatehouse: bool, watch_tower: bool,
		brazier: bool) -> void:
	var rings := mini(maxi(walls, 1), 3)
	var sy := 1.0 + 0.2 * (rings - 1)
	var corners := {}
	for i in edges.size():
		var e: Array = edges[i]
		var hi: int = e[0]
		var k: int = e[1]
		var nb: int = e[2]
		if nb >= 0 and w.is_water(nb):
			continue                 # the waterfront stays open to the quay and the boats
		var ca := _inset_corner(hi, k, k, used, inset)
		var cb := _inset_corner(hi, k, k + 1, used, inset)
		var a: Vector2 = ca[0]
		var b: Vector2 = cb[0]
		for cr in [ca, cb]:
			var key := "%d_%d" % [roundi((cr[0] as Vector2).x * 200.0), roundi((cr[0] as Vector2).y * 200.0)]
			if not corners.has(key):
				corners[key] = [cr[0], cr[1], (_hex[hi].c as Vector2)]
		var gate := i == gate_i
		var gw := 0.0
		if gate:
			gw = (0.308 if gatehouse else 0.164) * WALL_SCALE if tier.begins_with("stone") else 0.2
		_edge_pieces(tier, a, b, gate, gw, gatehouse, sy)
	if tier.begins_with("stone"):
		for key in corners:
			var cr: Array = corners[key]
			if rings >= 2 and bool(cr[1]) or rings >= 3:
				_tower(cr[0], cr[2], rings, "perimeter", tier)
	if watch_tower:
		# the watch post becomes a wall tower once the village is walled: on the outline corner nearest the Watch quarter
		var wd := _dir(_front - 2)
		var wbest := -INF
		var wat := Vector2.INF
		var whc := _c0
		for key in corners:
			var cr: Array = corners[key]
			var sc := ((cr[0] as Vector2) - _c0).normalized().dot(wd) + (0.3 if bool(cr[1]) else 0.0)
			if sc > wbest and wb._river_dist(cr[0]) > 0.1:
				wbest = sc
				wat = cr[0]
				whc = cr[2]
		if wat != Vector2.INF:
			var has_tower := rings >= 2
			if not has_tower:
				_tower(wat, whc, rings, "watch", "wall_tower")
			else:
				for it in _items:
					if it.kind == "tower" and Vector2(it.x, it.z).distance_to(wat) < 0.01:
						it.chain = "watch"
						it.tier = "wall_tower"
			var top := w.height_at(wat) + 0.153 * WALL_SCALE * (1.0 + 0.15 * (rings - 1))
			wb.props.add("banner_small%d" % _civ, wb._xf(wat, top, -0.4, 1.0), Color(_cc.r, _cc.g, _cc.b, _seed))
			if brazier:
				wb.props.add("flame", wb._xf(wat, top, 0.0, 0.7), Color(1, 1, 1, _seed))

func _tower(p: Vector2, hc: Vector2, rings: int, chain: String, tier: String) -> void:
	if w.tile_at(p) < 0 or not w.is_land(w.tile_at(p)) or wb._river_dist(p) < 0.08:
		return
	var sc := WALL_SCALE * (1.0 + 0.15 * (rings - 1))
	var ang := atan2((p - hc).y, (p - hc).x)
	var tb := Basis(Vector3.UP, -(ang + PI * 0.5)) * Basis.from_scale(Vector3(WALL_SCALE, sc, WALL_SCALE))
	wb.props.add("wall_tower", Transform3D(tb, Vector3(p.x, w.height_at(p), p.y)), Color(1, 1, 1, _seed))
	_record({"kind": "tower", "mesh": "wall_tower", "quarter": "perimeter", "slot": "corner", "chain": chain, "tier": tier, "slot_hex": w.tile_at(p),
		"x": p.x, "z": p.y, "r": 0.045 * WALL_SCALE * 1.4142, "perimeter": true})

## Pieces along one inset edge from a to b (the village inside to the left of a->b): wall runs, palisade stakes or thorn bushes, with the gate
## (or gatehouse) in the middle of the gate edge.
func _edge_pieces(tier: String, a: Vector2, b: Vector2, gate: bool, gw: float, gatehouse: bool, sy: float) -> void:
	var d := b - a
	var length := d.length()
	if length < 0.02:
		return
	var u := d / length
	var phi := atan2(u.y, u.x)
	var runs := []          # [from, to] stretches of this edge to fill
	if gate:
		var m := (a + b) * 0.5
		runs.append([a, m - u * gw * 0.5])
		runs.append([m + u * gw * 0.5, b])
		if tier.begins_with("stone"):
			var mesh_name := "wall_gatehouse" if gatehouse else "wall_gate"
			var gb := Basis(Vector3.UP, -phi) * Basis.from_scale(Vector3(WALL_SCALE, WALL_SCALE * sy, WALL_SCALE))
			wb.props.add(mesh_name, Transform3D(gb, Vector3(m.x, w.height_at(m), m.y)), Color(1, 1, 1, _seed))
			var f := _foot(mesh_name)
			_record({"kind": "gate", "mesh": mesh_name, "quarter": "perimeter", "slot": "gate", "chain": "gate", "tier": "gatehouse" if gatehouse else "gate",
				"slot_hex": w.tile_at(m), "x": m.x, "z": m.y, "r": 0.0, "perimeter": true,
				"box": [m.x - sin(phi) * float(f[1].y) * WALL_SCALE, m.y + cos(phi) * float(f[1].y) * WALL_SCALE, float(f[2].x) * WALL_SCALE, float(f[2].y) * WALL_SCALE, phi]})
	else:
		runs.append([a, b])
	for run in runs:
		var ra: Vector2 = run[0]
		var rb: Vector2 = run[1]
		var rl := ra.distance_to(rb)
		if rl < 0.03:
			continue
		match tier:
			"thorn":
				var n := maxi(1, int(ceil(rl / 0.105)))
				for i in n:
					var q := ra.lerp(rb, (i + 0.5) / n)
					if not _piece_ok(q) or M7WorldData.hash01(_t, i + roundi(q.x * 31.0), 10) < 0.04:
						continue
					var s := 1.0 + 0.1 * M7WorldData.hash01(_t, i, 8)
					wb.props.add("thorn", wb._xf(q, w.height_at(q), -phi, s), Color(1, 1, 1, M7WorldData.hash01(_t, i, 9)))
					_piece_record("thorn", q, phi, Vector2(0.077, 0.046) * s, tier)
			"palisade":
				var n := maxi(1, int(ceil(rl / 0.13)))
				for i in n:
					var q := ra.lerp(rb, (i + 0.5) / n)
					if not _piece_ok(q):
						continue
					var sx := (rl / n) / 0.126 * 1.02
					var pb := Basis(Vector3.UP, -phi) * Basis.from_scale(Vector3(sx, 1.1, 1.1))
					wb.props.add("decor_palisade", Transform3D(pb, Vector3(q.x, w.height_at(q), q.y)), Color(_cc.r, _cc.g, _cc.b, M7WorldData.hash01(_t, 10, 11)))
					_piece_record("decor_palisade", q, phi, Vector2(0.063 * sx, 0.011 * 1.1), tier)
			_:
				var n := maxi(1, roundi(rl / (SEG * WALL_SCALE)))
				for i in n:
					var q := ra.lerp(rb, (i + 0.5) / n)
					if not _piece_ok(q):
						continue
					var sx := (rl / n) / SEG * 1.04
					var segb := Basis(Vector3.UP, -phi) * Basis.from_scale(Vector3(sx, WALL_SCALE * sy, WALL_SCALE))
					wb.props.add("wall_seg", Transform3D(segb, Vector3(q.x, w.height_at(q), q.y)), Color(1, 1, 1, _seed))
					_piece_record("wall_seg", q, phi, Vector2(SEG * 0.5 * sx, 0.022 * WALL_SCALE), tier)

func _piece_ok(q: Vector2) -> bool:
	var tq := w.tile_at(q)
	return tq >= 0 and w.is_land(tq) and wb._river_dist(q) >= 0.08

func _piece_record(mesh_name: String, q: Vector2, phi: float, half: Vector2, tier: String) -> void:
	_record({"kind": "perimeter", "mesh": mesh_name, "quarter": "perimeter", "slot": "edge", "chain": "perimeter", "tier": tier, "slot_hex": w.tile_at(q),
		"x": q.x, "z": q.y, "r": half.length(), "perimeter": true, "box": [q.x, q.y, half.x, half.y, phi]})

# ------------------------------------------------------------------ the water front (§4.2 Front): weir, or the harbor quay and its boats

func _waterfront(s: Dictionary, st: Array) -> void:
	var K := M7WorldData.Kind
	var harbor := false
	if w.iron_works(s, "harbor") > 0:
		var before := wb.harbor_sites.size()
		harbor = wb._harbor(s, _t, _c0, _seed, _cc, _front)
		if harbor and wb.harbor_sites.size() > before:
			var hs: Dictionary = wb.harbor_sites[-1]
			var hp: Vector2 = hs.p
			var dir := (hp - _c0).normalized()
			var yaw := atan2(-dir.x, -dir.y)
			var f := _foot("harbor")
			var o: Vector3 = M7WorldBuilder.rot_y(yaw) * Vector3(f[1].x, 0.0, f[1].y) * 1.15
			_record({"kind": "harbor", "mesh": "harbor", "quarter": "front", "slot": "water", "chain": "water", "tier": "harbor", "slot_hex": w.tile_at(hp),
				"x": hp.x, "z": hp.y, "r": 0.0, "box": [hp.x + o.x, hp.y + o.z, float(f[2].x) * 1.15, float(f[2].y) * 1.15, -yaw]})
	if not harbor and int(st[K.WEIR]) > 0:
		var placed := false
		if _water_front:
			var nb := w.neighbour(_t, _front)
			var tc := w.tile_center(nb)
			var q := (_c0 + tc) * 0.5 + (tc - _c0) * 0.12
			var yaw := atan2((tc - _c0).x, (tc - _c0).y)
			wb.props.add("weir", wb._xf(q, -0.02, yaw, 1.0), Color(1, 1, 1, _seed))
			_record({"kind": "weir", "mesh": "weir", "quarter": "front", "slot": "water", "chain": "water", "tier": "weir", "slot_hex": w.tile_at(q),
				"x": q.x, "z": q.y, "r": 0.0, "box": [q.x, q.y, 0.155, 0.067, -yaw]})
			placed = true
		if not placed:
			var best := 1e9
			var bq := Vector2.ZERO
			var bd := Vector2.ZERO
			for sgi in wb.river_segs:
				var a2: Vector2 = sgi[0]
				if a2.distance_to(_c0) > 0.9:
					continue
				var dd := absf(a2.distance_to(_c0) - 0.42)
				if dd < best:
					best = dd
					bq = a2
					bd = (sgi[1] as Vector2) - a2
			if best < 0.3:
				var yaw := atan2(bd.x, bd.y)
				wb.props.add("weir", wb._xf(bq, w.height_at(bq) + 0.02, yaw, 0.55), Color(1, 1, 1, _seed))
				_record({"kind": "weir", "mesh": "weir", "quarter": "front", "slot": "river", "chain": "water", "tier": "weir", "slot_hex": w.tile_at(bq),
					"x": bq.x, "z": bq.y, "r": 0.0, "box": [bq.x, bq.y, 0.155 * 0.55, 0.067 * 0.55, -yaw]})
	var civ := _civ
	var sea: int = w.sea_tier[civ] if civ >= 0 and civ < w.sea_tier.size() else 0
	if sea > 0:
		wb._moor_fleet(_t, _c0, _seed, sea, int(s.population), _cc, harbor, _front)

# ------------------------------------------------------------------ outer works (§4.7)

func _outer_candidates() -> Array:
	var seen := {}
	for hx in _hex:
		seen[int(hx.tile)] = true
	var out := []
	for d in 6:
		var r1 := int(_hex[d + 1].tile)
		if r1 < 0:
			continue
		for k in 6:
			var nb := w.neighbour(r1, (k + _front) % 6)
			if nb >= 0 and not seen.has(nb) and not _sim_claimed_tiles.has(nb):
				seen[nb] = true
				out.append(nb)
	return out

func _outer(s: Dictionary, si: int, st: Array, decor: Array, aqueduct_at) -> void:
	var K := M7WorldData.Kind
	var tint := Color(_cc.r, _cc.g, _cc.b, _seed)
	var ring2 := _outer_candidates()
	var taken := {}
	# the aqueduct's spans first (a line feature); nothing else stands on a tile they cross
	if aqueduct_at != null:
		_aqueduct_spans(aqueduct_at)
		for it in _items:
			if it.kind == "aqueduct_span":
				taken[int(it.slot_hex)] = true
	# the pasture paddock: a ring-2 tile with no plot, grassland first
	if int(st[K.PASTURE]) > 0:
		var best := -1
		var best_score := -1
		for tile in ring2:
			if not w.is_land(tile) or w.plot_kind[tile] >= 0 or w.biome[tile] >= M7WorldData.Biome.MOUNTAIN or (w.flags[tile] & M7WorldData.FLAG_RIVER) != 0 or taken.has(tile):
				continue
			var b := int(w.biome[tile])
			var score := 2 if b == M7WorldData.Biome.GRASSLAND or b == M7WorldData.Biome.SAVANNA else (1 if b == M7WorldData.Biome.OPEN_WOODLAND else 0)
			if score > best_score:
				best_score = score
				best = tile
		if best >= 0:
			taken[best] = true
			_paddock(best)
	# the shrine of the old layout stood outside the village; it is in the Sacred quarter now. Markers its camps do not carry, and the
	# charcoal clamp and quarry, stand on ring-2 tiles with no plot.
	var loose := mini(wb._built(st, K.MARKER), M7WorldBuilder.MARKERS_SHOWN) - wb._seasonal_camps_of(int(s.get("index", -1)))
	var extras := []
	for k in maxi(loose, 0):
		extras.append(["marker", "marker", 1.3])
	if decor.has("charcoal"):
		extras.append(["charcoal", "decor_charcoal", CORE_VS])
	if decor.has("quarry"):
		extras.append(["quarry", "decor_quarry", CORE_VS])
	for ex in extras:
		var spot := -1
		for tile in ring2:
			if taken.has(tile) or not w.is_land(tile) or w.plot_kind[tile] >= 0 or w.biome[tile] >= M7WorldData.Biome.PEAK:
				continue
			if ex[0] == "charcoal" and not (w.biome[tile] in [M7WorldData.Biome.TEMPERATE_FOREST, M7WorldData.Biome.TAIGA, M7WorldData.Biome.TROPICAL_FOREST, M7WorldData.Biome.OPEN_WOODLAND]) and spot < 0:
				continue
			spot = tile
			break
		if spot < 0:
			for tile in ring2:
				if not taken.has(tile) and w.is_land(tile) and w.plot_kind[tile] < 0 and w.biome[tile] < M7WorldData.Biome.PEAK:
					spot = tile
					break
		if spot < 0:
			_dropped.append(String(ex[0]))
			continue
		taken[spot] = true
		var p := w.tile_center(spot)
		if wb._river_dist(p) < 0.15:
			p += (_c0 - p).normalized() * 0.2
		var yaw := atan2((_c0 - p).x, (_c0 - p).y)
		wb.props.add(String(ex[1]), wb._xf(p, w.height_at(p), yaw, float(ex[2])), tint)
		var f := _foot(String(ex[1]))
		_record({"kind": String(ex[0]), "mesh": String(ex[1]), "quarter": "outer", "slot": "ring2", "chain": "", "tier": "", "slot_hex": spot,
			"x": p.x, "z": p.y, "r": float(f[0]) * float(ex[2]), "outer": true})
	if w.iron_works(s, "water_mill") > 0:
		var before := wb.props.instance_count
		var mills_before: int = _count_groups("water_mill")
		var mill_clearance: Array = []
		for item in _items:
			var radius := float(item.get("r", 0.0))
			if item.has("box"):
				var box: Array = item["box"]
				radius = maxf(radius, Vector2(float(box[2]), float(box[3])).length())
			mill_clearance.append([Vector2(float(item.x), float(item.z)), radius])
		wb._water_mill(s, _c, _h0, _seed, 1.15, [], tint, _sim_claimed_tiles, mill_clearance)
		if _count_groups("water_mill") > mills_before:
			var mill_at := _last_xf("water_mill")
			_record({"kind": "water_mill", "mesh": "water_mill", "quarter": "outer", "slot": "river", "chain": "grinding", "tier": "water_mill", "slot_hex": w.tile_at(mill_at),
				"x": mill_at.x, "z": mill_at.y, "r": 0.3, "outer": true})
	if w.iron_works(s, "great_lighthouse") > 0:
		_lighthouse(ring2)
	wb._frontier_walls(s, si, _c, _h0, _seed, 1.25)
	var siege: Dictionary = wb._siege_info(s, _c, _seed)
	if not siege.is_empty():
		wb._siege_camp(_c, _seed, 1.3, siege, _cc)

func _count_groups(mesh_name: String) -> int:
	var n := 0
	for key in wb.props.groups:
		var g: Dictionary = wb.props.groups[key]
		if String(g.mesh) == mesh_name:
			n += int(g.count)
	return n

func _last_xf(mesh_name: String) -> Vector2:
	for key in wb.props.groups:
		var g: Dictionary = wb.props.groups[key]
		if String(g.mesh) == mesh_name and int(g.count) > 0:
			var buf: PackedFloat32Array = g.buffer
			var o := buf.size() - 16
			return Vector2(buf[o + 3], buf[o + 11])
	return Vector2.ZERO

func _paddock(tile: int) -> void:
	var pc := w.tile_center(tile)
	var ph: float = w.heights[tile]
	var pr := 0.3
	for e in 6:
		var a0 := M7WorldData.corner(e) / M7WorldData.R * pr
		var a1 := M7WorldData.corner((e + 1) % 6) / M7WorldData.R * pr
		for half in 2:
			var q := pc + a0.lerp(a1, 0.25 + 0.5 * half)
			var dd := a1 - a0
			wb.props.add("fence", wb._xf(q, ph, -atan2(dd.y, dd.x), 1.15), Color(1, 1, 1, 0.5))
	var wool := w.has_look(_civ, M7WorldData.LOOK_WOOL)
	for j in (10 if wool else 4):
		var q := pc + Vector2(cos(j * 1.7 + _seed), sin(j * 1.7 + _seed)) * (0.06 + 0.04 * j)
		if wool:
			q = pc + Vector2(cos(j * 2.39996 + _seed), sin(j * 2.39996 + _seed)) * (0.045 * sqrt(j + 0.6))
		var dark := j == 2 or (wool and j == 7)
		wb.props.add("animal_woolly", wb._xf(q, ph, j * 2.1 + _seed, 1.14 if wool else 1.0),
			M7WorldBuilder._glide_custom((6 * 8 + 4) if not dark else (9 * 8 + 3), 0, 0.5, 0.0, Vector2.ZERO))
		wb._record_visual_occupancy(pc, 0.32)
	_record({"kind": "paddock", "mesh": "fence", "quarter": "outer", "slot": "ring2", "chain": "", "tier": "", "slot_hex": tile,
		"x": pc.x, "z": pc.y, "r": 0.32, "outer": true})

## The Great Lighthouse (SEA-08) on the shore of a ring-2 tile beside the sea, nearest the Front.
func _lighthouse(ring2: Array) -> void:
	var best := -1
	var best_dot := -INF
	var shore := Vector2.ZERO
	for tile in ring2:
		if not w.is_land(tile) or w.plot_kind[tile] >= 0 or w.biome[tile] >= M7WorldData.Biome.MOUNTAIN:
			continue
		for k in 6:
			var nb := w.neighbour(tile, k)
			if nb >= 0 and w.is_water(nb):
				var dot := (w.tile_center(tile) - _c0).normalized().dot(_dir(_front))
				if dot > best_dot:
					best_dot = dot
					best = tile
					shore = w.tile_center(tile) + _dir(k) * 0.22
				break
	if best < 0:
		return
	var y := maxf(w.height_at(shore), 0.05)
	wb.props.add("great_lighthouse", wb._xf(shore, y, 0.0, M7WorldBuilder.LIGHTHOUSE_SCALE), Color(1, 1, 1, _seed))
	wb._record_visual_occupancy(shore, 0.42)
	wb.beacon_fires.append({"top": Vector3(shore.x, y + 0.74 * M7WorldBuilder.LIGHTHOUSE_SCALE, shore.y), "seed": _seed})
	wb.lighthouse_sites.append({"p": shore, "y": y})
	_record({"kind": "lighthouse", "mesh": "great_lighthouse", "quarter": "outer", "slot": "shore", "chain": "", "tier": "", "slot_hex": best,
		"x": shore.x, "z": shore.y, "r": 0.3, "outer": true})

## The aqueduct's spans: from outside the perimeter of the hex holding its water tower, straight out across ring 2.
func _aqueduct_spans(at: Array) -> void:
	var hi: int = at[0]
	var u: Vector2 = ((_hex[hi].c as Vector2) - _c0).normalized() if hi > 0 else _dir(_front)
	var start: Vector2 = (_hex[hi].c as Vector2) + u * 0.56 if hi > 0 else _c0 + u * 0.56
	var sc := M7WorldBuilder.AQUEDUCT_SCALE
	var run := 0.3 * sc
	var grounds := []
	var level := -INF
	for j in 3:
		var p := start + u * run * (j + 0.5)
		var tq := w.tile_at(p)
		if tq < 0 or _sim_claimed_tiles.has(tq) or not w.is_land(tq):
			break
		grounds.append([p, w.height_at(p)])
		level = maxf(level, w.height_at(p))
	if grounds.is_empty():
		return
	var deck := level + 0.16 * sc
	var yaw := -atan2(u.y, u.x)
	for g in grounds:
		var p: Vector2 = g[0]
		var gy: float = g[1]
		var sy := clampf((deck - gy) / (0.14 * sc), 0.5, 4.0)
		wb.props.add("aqueduct_run", Transform3D(Basis(Vector3.UP, yaw) * Basis.from_scale(Vector3(sc, sc * sy, sc)), Vector3(p.x, gy, p.y)), Color(1, 1, 1, _seed))
		_record({"kind": "aqueduct_span", "mesh": "aqueduct_run", "quarter": "outer", "slot": "span", "chain": "", "tier": "", "slot_hex": w.tile_at(p),
			"x": p.x, "z": p.y, "r": 0.0, "outer": true, "box": [p.x, p.y, 0.15 * sc, 0.05 * sc, atan2(u.y, u.x)]})
	wb.aqueduct_sites.append({"p": start + u * 0.6, "y": w.height_at(start)})

# ------------------------------------------------------------------ the dump

func write_dump(path: String) -> void:
	var out := {"width": w.width, "height": w.height, "tick": w.tick, "petal": PETAL, "small_r": SMALL_R, "half_r": HALF_R, "whole_r": WHOLE_R,
		"plots": Array(w.plots), "core_district_ids": w.core_district_ids, "settlements": _rows}
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		push_error("cannot write layout dump " + path)
		return
	f.store_string(JSON.stringify(out, " "))
	f.close()
