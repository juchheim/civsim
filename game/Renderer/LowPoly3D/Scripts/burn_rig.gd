## Burning landscapes: Landscape Burning (FIR-05) and Slash-and-Burn (AGR-04).
##
## The simulation has no controlled burns, no burnt ground and no clearing; the frame only says when each civilization learned the
## two projects (MatchFrame.CivBurnLearned) and where its villages stand. So the burning is played from those: every autumn each
## village of a civilization that knows FIR-05 sets a patch of dry grass alight near it, the flame walking across it in about nine
## seconds, and every fourth year a village that knows AGR-04 burns a patch of forest edge. Where it has burnt, the grass (and for
## slash-and-burn the trees) vanish from the ground, the soil is black and glowing, then grey, and the land recovers over the
## following years (green flush for three years after a grass fire; trees back after eight). Everything is a pure function of the
## render tick and the frame, so pausing, speed and loading all work and nothing is stored. The effects the project text promises
## (more forage on burnt tiles, a 5 % wildfire risk) are not simulated.
##
## The scars live in a world-space texture (the `burn_map` global) read by the terrain and by grass and tree meshes (prop.gdshader).
class_name M7BurnRig
extends RefCounted

const TEXELS := 3.0                 # burn-map texels per world unit
const YEAR := 600.0
const AUTUMN := 300.0               # ticks into the year when autumn starts (a season is 150)
const FRONT_TICKS := 90.0           # a patch burns across over this long
const GLOW_RISE := 20.0
const GLOW_END := 170.0
const CHAR_RISE := 30.0
const CHAR_HOLD := 600.0
const CHAR_END := 1800.0            # the scar is gone after three years
const GRASS_BACK_FROM := 300.0
const GRASS_BACK_END := 900.0
const TREE_BACK_FROM := 2400.0
const TREE_BACK_END := 4800.0       # trees are back after eight years
const FLUSH_FROM := 500.0
const FLUSH_END := 1800.0
const CONTROLLED_YEARS := 3
const SLASH_YEARS := 8
const MAX_FLAMES := 160
const MAX_SMOKES := 6
const MAX_LIGHTS := 4

var b                               # the M7WorldBuilder
var parent: Node3D
var smoke_factory: Callable
var holder: Node3D
var events: Array = []              # {kind, tiles, ignite, center, first, last}
var signature := -1

var image: Image
var texture: ImageTexture
var data := PackedByteArray()
var tex_w := 1
var tex_h := 1
var fx_focus := Vector2.ZERO        # where the fire is now (dev scenarios aim the camera at it)
var fx_active := false
var cur := {}                       # tile -> Vector4(char, grass, trees, a-encoded) for the last frame

var flames: Array = []
var smokes: Array = []
var lights: Array = []

## An empty map, so shaders never see an unset texture (white would read as "everything burnt").
static func install_empty() -> void:
	var img := Image.create(2, 2, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))
	RenderingServer.global_shader_parameter_set("burn_map", ImageTexture.create_from_image(img))
	RenderingServer.global_shader_parameter_set("burn_rect", Vector4(0, 0, 1, 1))

func _init(builder, parent_node: Node3D, smoke_maker: Callable) -> void:
	b = builder
	parent = parent_node
	smoke_factory = smoke_maker
	var w: M7WorldData = b.w
	var span := Vector2(w.width + 0.5, w.height * M7WorldData.S)
	tex_w = int(ceil(span.x * TEXELS))
	tex_h = int(ceil(span.y * TEXELS))
	image = Image.create(tex_w, tex_h, false, Image.FORMAT_RGBA8)
	data = image.get_data()
	texture = ImageTexture.create_from_image(image)
	RenderingServer.global_shader_parameter_set("burn_map", texture)
	RenderingServer.global_shader_parameter_set("burn_rect", Vector4(0, 0, 1.0 / span.x, 1.0 / span.y))
	holder = Node3D.new()
	holder.name = "Burning"
	parent.add_child(holder)
	for i in MAX_FLAMES:
		var flame := MeshInstance3D.new()
		flame.mesh = b.kit.get_mesh("flame")
		flame.material_override = b.mats.fire
		flame.visible = false
		holder.add_child(flame)
		flames.append(flame)
	for i in MAX_SMOKES:
		var smoke: GPUParticles3D = smoke_factory.call()
		M7SignalRig.thicken_smoke(smoke, 0.14, 36, 7.0)
		smoke.emitting = false
		holder.add_child(smoke)
		smokes.append(smoke)
	for i in MAX_LIGHTS:
		var light := OmniLight3D.new()
		light.light_color = Color("#ff7a2a")
		light.omni_range = 1.1
		light.omni_attenuation = 1.4
		light.shadow_enabled = false
		light.visible = false
		holder.add_child(light)
		lights.append(light)

# ------------------------------------------------------------------ the schedule

static func _is_grass(biome: int) -> bool:
	return biome == M7WorldData.Biome.GRASSLAND or biome == M7WorldData.Biome.SAVANNA or biome == M7WorldData.Biome.OPEN_WOODLAND

## `--burn-debug`: a slash-and-burn patch may go on any tile with trees (grassland and savanna have some), to look at trees burning away.
static var DEBUG_FOREST: bool = "--burn-debug" in OS.get_cmdline_user_args()

static func _is_forest(biome: int) -> bool:
	if DEBUG_FOREST:
		return biome == M7WorldData.Biome.SAVANNA or biome == M7WorldData.Biome.OPEN_WOODLAND or biome == M7WorldData.Biome.GRASSLAND
	return biome == M7WorldData.Biome.TEMPERATE_FOREST or biome == M7WorldData.Biome.TAIGA or biome == M7WorldData.Biome.TROPICAL_FOREST

## Tiles within `near` to `far` steps of a village.
func _ring_tiles(start: int, near: int, far: int) -> Array:
	var w: M7WorldData = b.w
	var seen := {start: 0}
	var frontier := [start]
	var out: Array = []
	for d in range(1, far + 1):
		var next: Array = []
		for t in frontier:
			for nb in w.neighbours(t):
				if nb >= 0 and not seen.has(nb):
					seen[nb] = d
					next.append(nb)
					if d >= near:
						out.append(nb)
		frontier = next
	return out

## Where a patch may go. Only things that never change (land, the tile's kind, its distance from the village) decide this: a past
## year's patch must stand where it did, whatever the claims and fields have become since. Within three tiles of a village is inside
## the ground it holds from the first eras on (claim radius 3 to 4).
func _usable(tile: int) -> bool:
	return (b.w as M7WorldData).is_land(tile)

## One patch: a centre tile of the right kind and up to four neighbours of the same kind, with a time to ignite each so the flame
## walks across them.
func _patch(st: int, civ: int, year: int, forest: bool) -> Dictionary:
	var w: M7WorldData = b.w
	var candidates: Array = []
	for t in _ring_tiles(st, 1, 3):
		var ok: bool = _is_forest(w.biome[t]) if forest else _is_grass(w.biome[t])
		if ok and _usable(t):
			candidates.append(t)
	if candidates.is_empty():
		return {}
	var centre: int = candidates[int(M7WorldData.hash01(st, year, 61 if not forest else 63) * candidates.size()) % candidates.size()]
	var tiles := [centre]
	for nb in w.neighbours(centre):
		if tiles.size() >= (5 if not forest else 3):
			break
		if nb >= 0 and (_is_forest(w.biome[nb]) if forest else _is_grass(w.biome[nb])) and _usable(nb):
			tiles.append(nb)
	var base := year * YEAR + AUTUMN + 20.0 + 80.0 * M7WorldData.hash01(st, year, 62) + (60.0 if forest else 0.0)
	var angle := M7WorldData.hash01(st, year, 64) * TAU
	var dir := Vector2(cos(angle), sin(angle))
	var proj := PackedFloat32Array()
	var lo := 1e9
	var hi := -1e9
	for t in tiles:
		var v := (w.tile_center(t) as Vector2).dot(dir)
		proj.append(v)
		lo = minf(lo, v)
		hi = maxf(hi, v)
	var ignite := PackedFloat32Array()
	var packed := PackedInt32Array()
	var sum := Vector2.ZERO
	for i in tiles.size():
		packed.append(tiles[i])
		ignite.append(base + FRONT_TICKS * (proj[i] - lo) / maxf(hi - lo, 0.001))
		sum += w.tile_center(tiles[i])
	return {"kind": 1 if forest else 0, "tiles": packed, "ignite": ignite, "centre": sum / tiles.size(), "first": base, "last": base + FRONT_TICKS}

func _rebuild(year_now: int) -> void:
	events.clear()
	var w: M7WorldData = b.w
	for s in w.settlements:
		if bool(s.get("remembered")) or int(s.population) <= 0:
			continue
		var civ := int(s.civ)
		var st := int(s.tile)
		var fire_at: int = w.burn_learned[civ * 2] if civ >= 0 and civ * 2 < w.burn_learned.size() else -1
		var slash_at: int = w.burn_learned[civ * 2 + 1] if civ >= 0 and civ * 2 + 1 < w.burn_learned.size() else -1
		if fire_at >= 0:
			for y in range(maxi(year_now - CONTROLLED_YEARS, 0), year_now + 1):
				var e := _patch(st, civ, y, false)
				if not e.is_empty() and float(e.first) >= fire_at:
					events.append(e)
		if slash_at >= 0:
			for y in range(maxi(year_now - SLASH_YEARS, 0), year_now + 1):
				if (y + st % 4) % 4 != 0:
					continue
				var e := _patch(st, civ, y, true)
				if not e.is_empty() and float(e.first) >= slash_at:
					events.append(e)

func sync(tick: float) -> void:
	var w: M7WorldData = b.w
	var year_now := int(tick / YEAR)
	var sig := hash(w.burn_learned) * 31 + year_now
	for s in w.settlements:
		sig = sig * 31 + int(s.tile) * 7 + int(s.civ) + (1 if int(s.population) > 0 and not bool(s.get("remembered")) else 0)
	if sig == signature:
		return
	signature = sig
	_rebuild(year_now)
	data.fill(0)
	cur.clear()
	_push()

# ------------------------------------------------------------------ the state of the ground at a given time

static func _ramp(x: float, a: float, b2: float) -> float:
	return clampf((x - a) / (b2 - a), 0.0, 1.0)

## [char, grass gone, trees gone, glow, flush] for a tile `age` ticks after it ignited.
static func _state(kind: int, age: float) -> Array:
	var glow := 0.0
	if age < GLOW_END:
		glow = _ramp(age, 0.0, GLOW_RISE) * (1.0 - _ramp(age, GLOW_RISE, GLOW_END))
	var scar := _ramp(age, 0.0, CHAR_RISE) * (1.0 if age < CHAR_HOLD else 1.0 - _ramp(age, CHAR_HOLD, CHAR_END))
	var grass := 1.0 - _ramp(age, GRASS_BACK_FROM, GRASS_BACK_END)
	var trees := 0.0
	if kind == 1:
		trees = 1.0 - _ramp(age, TREE_BACK_FROM, TREE_BACK_END)
	var flush := 0.0
	if kind == 0 and age > FLUSH_FROM and age < FLUSH_END:
		flush = sin(PI * _ramp(age, FLUSH_FROM, FLUSH_END))
	return [scar, grass, trees, glow, flush]

func update(tick: float) -> void:
	var w: M7WorldData = b.w
	var now := {}
	var glowing: Array = []                         # [tile, glow, event]
	for ei in events.size():
		var e: Dictionary = events[ei]
		if tick < float(e.first):
			continue
		var kind: int = e.kind
		var life: float = (SLASH_YEARS if kind == 1 else CONTROLLED_YEARS) * YEAR + 100.0
		if tick > float(e.last) + life:
			continue
		for i in (e.tiles as PackedInt32Array).size():
			var age := tick - (e.ignite as PackedFloat32Array)[i]
			if age < 0.0:
				continue
			var tile: int = (e.tiles as PackedInt32Array)[i]
			var st := _state(kind, age)
			var a: float = 0.5 + 0.5 * float(st[3]) if float(st[3]) > 0.02 else 0.5 * float(st[4])
			var prev: Vector4 = now.get(tile, Vector4.ZERO)
			now[tile] = Vector4(maxf(prev.x, st[0]), maxf(prev.y, st[1]), maxf(prev.z, st[2]), maxf(prev.w, a))
			if float(st[3]) > 0.05:
				glowing.append([tile, float(st[3]), ei, age])
	# only tiles whose quantised value changed are repainted
	var changed: Array = []
	for tile in now:
		var q := _quantise(now[tile])
		if not cur.has(tile) or cur[tile] != q:
			changed.append(tile)
		cur[tile] = q
	var gone: Array = []
	for tile in cur:
		if not now.has(tile):
			gone.append(tile)
	for tile in gone:
		cur.erase(tile)
		changed.append(tile)
	if not changed.is_empty():
		var done := {}
		for tile in changed:
			if not done.has(tile):
				_repaint(tile)
				done[tile] = true
		_push()
	_fx(glowing, tick)

static func _quantise(v: Vector4) -> Vector4:
	return Vector4(roundf(v.x * 24.0), roundf(v.y * 24.0), roundf(v.z * 24.0), roundf(v.w * 48.0)) / Vector4(24.0, 24.0, 24.0, 48.0)

## Repaints the texels around one tile from that tile and its neighbours (the map is the per-channel maximum of their soft discs).
func _repaint(tile: int) -> void:
	var w: M7WorldData = b.w
	var c: Vector2 = w.tile_center(tile)
	var around: Array = [tile]
	for nb in w.neighbours(tile):
		if nb >= 0 and cur.has(nb):
			around.append(nb)
	var x0 := maxi(int(floor((c.x - 0.62) * TEXELS)), 0)
	var x1 := mini(int(ceil((c.x + 0.62) * TEXELS)), tex_w - 1)
	var y0 := maxi(int(floor((c.y - 0.62) * TEXELS)), 0)
	var y1 := mini(int(ceil((c.y + 0.62) * TEXELS)), tex_h - 1)
	for ty in range(y0, y1 + 1):
		for tx in range(x0, x1 + 1):
			var p := Vector2((tx + 0.5) / TEXELS, (ty + 0.5) / TEXELS)
			var acc := Vector3.ZERO                       # char, grass gone, trees gone
			var glow := 0.0
			var flush := 0.0
			for u in around:
				if not cur.has(u):
					continue
				var d := p.distance_to(w.tile_center(u))
				var weight := 1.0 - smoothstep(0.36, 0.62, d)
				if weight <= 0.0:
					continue
				var v: Vector4 = cur[u]
				acc = Vector3(maxf(acc.x, v.x * weight), maxf(acc.y, v.y * weight), maxf(acc.z, v.z * weight))
				if v.w > 0.5:
					glow = maxf(glow, (v.w - 0.5) * 2.0 * weight)
				else:
					flush = maxf(flush, v.w * 2.0 * weight)
			var enc := 0.5 + 0.5 * glow if glow > 0.02 else 0.5 * flush
			var o := (ty * tex_w + tx) * 4
			data[o] = int(acc.x * 255.0)
			data[o + 1] = int(acc.y * 255.0)
			data[o + 2] = int(acc.z * 255.0)
			data[o + 3] = int(enc * 255.0)

func _push() -> void:
	image.set_data(tex_w, tex_h, false, Image.FORMAT_RGBA8, data)
	texture.update(image)

# ------------------------------------------------------------------ flames, smoke and light at the front

func _fx(glowing: Array, tick: float) -> void:
	var w: M7WorldData = b.w
	var used := 0
	var by_event := {}
	for g in glowing:
		var tile: int = g[0]
		var glow: float = g[1]
		var c: Vector2 = w.tile_center(tile)
		for k in 5:
			if used >= MAX_FLAMES:
				break
			var node: MeshInstance3D = flames[used]
			var seed := M7WorldData.hash01(tile, k, 71)
			var p := c + Vector2(seed - 0.5, M7WorldData.hash01(tile, k, 72) - 0.5) * 0.7
			var flicker := 0.75 + 0.25 * sin(tick * 0.9 + seed * 20.0)
			var s := 0.7 + 1.3 * glow * flicker
			node.position = Vector3(p.x, maxf(w.height_at(p), 0.0), p.y)
			node.scale = Vector3(s, s * (0.9 + 0.6 * glow), s)
			node.visible = true
			used += 1
		var key: int = g[2]
		var acc: Array = by_event.get(key, [Vector2.ZERO, 0, 0.0])
		acc[0] += c * glow
		acc[1] += 1
		acc[2] += glow
		by_event[key] = acc
	for i in range(used, MAX_FLAMES):
		(flames[i] as MeshInstance3D).visible = false
	fx_active = not by_event.is_empty()
	var slot := 0
	for key in by_event:
		if slot >= MAX_SMOKES:
			break
		var acc: Array = by_event[key]
		var weight: float = acc[2]
		var centre: Vector2 = acc[0] / maxf(weight, 0.001)
		fx_focus = centre
		var y := maxf(w.height_at(centre), 0.0)
		var smoke: GPUParticles3D = smokes[slot]
		smoke.position = Vector3(centre.x, y + 0.08, centre.y)
		smoke.emitting = true
		smoke.amount_ratio = clampf(0.3 + 0.2 * weight, 0.3, 1.0)
		if slot < MAX_LIGHTS:
			var light: OmniLight3D = lights[slot]
			light.visible = true
			light.position = Vector3(centre.x, y + 0.25, centre.y)
			light.light_energy = clampf(0.4 + 0.35 * weight, 0.4, 1.6)
		slot += 1
	for i in range(slot, MAX_SMOKES):
		(smokes[i] as GPUParticles3D).emitting = false
	for i in range(mini(slot, MAX_LIGHTS), MAX_LIGHTS):
		(lights[i] as OmniLight3D).visible = false
