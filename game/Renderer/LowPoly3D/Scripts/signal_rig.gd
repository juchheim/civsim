## Signal fires (COM-04): a post on the highest ground every few tiles along each village-to-village trail of a civilization that
## knows the project, so the news can run from hill to hill. The sim only says who knows it (MatchFrame.CivSignals); the posts are
## laid out here from the trails the frame already carries, so they stand where the trails run and nowhere unexplored.
##
## Each post keeps a low fire and a thin thread of smoke. Every so often a flare runs down the chain, post by post: the flame
## leaps, a light kindles and the smoke thickens, then each post settles back. Everything is a function of the clock.
class_name M7SignalRig
extends RefCounted

const POST_SCALE := 1.6
const MIN_RUN := 8                  # a trail shorter than this has no room for a post
const SPACING := 5                  # tiles between posts along a trail
const END_MARGIN := 3               # stay this many tiles from a village at either end
const MIN_APART := 0.9              # world units between any two posts
const MIN_FROM_VILLAGE := 0.8
const PERIOD := 16.0                # seconds between flares
const STEP := 0.9                   # seconds a flare takes to pass from one post to the next
const FLARE := 3.2                  # how long one post stays flared

var b                               # the M7WorldBuilder
var parent: Node3D
var smoke_factory: Callable
var holder: Node3D
var posts: Array = []               # {pos, chain, index, flame, light, smoke, mound}
var signature := -1

func _init(builder, parent_node: Node3D, smoke_maker: Callable) -> void:
	b = builder
	parent = parent_node
	smoke_factory = smoke_maker

## Rebuilds the posts when the trails, who keeps fires, or what is explored changes.
func sync() -> void:
	var w: M7WorldData = b.w
	var sig := hash(w.trail_meta) * 31 + hash(w.trail_tiles) * 17 + hash(w.signal_civs)
	if sig == signature:
		return
	signature = sig
	if holder != null:
		holder.queue_free()
	holder = Node3D.new()
	holder.name = "SignalPosts"
	parent.add_child(holder)
	posts.clear()
	var chain_id := 0
	for ti in range(0, w.trail_meta.size(), 5):
		var civ := w.trail_meta[ti + 1]
		if w.trail_meta[ti + 2] != 0 or civ < 0 or civ >= w.signal_civs.size() or w.signal_civs[civ] == 0:
			continue
		var start := w.trail_meta[ti + 3]
		var length := w.trail_meta[ti + 4]
		var run := PackedInt32Array()
		for j in length:
			var tile := w.trail_tiles[start + j]
			if tile >= 0:
				run.append(tile)
			if (tile < 0 or j == length - 1) and run.size() > 0:
				_post_run(run, chain_id)
				chain_id += 1
				run = PackedInt32Array()

func _post_ok(tile: int, p: Vector2) -> bool:
	var w: M7WorldData = b.w
	if not w.is_land(tile) or w.plot_kind[tile] >= 0 or w.biome[tile] >= M7WorldData.Biome.PEAK:
		return false
	if b._river_dist(p) < 0.12:
		return false
	for hearth in b.hearths:
		if Vector2(hearth.x, hearth.z).distance_to(p) < MIN_FROM_VILLAGE:
			return false
	for post in posts:
		if (post.pos as Vector2).distance_to(p) < MIN_APART:
			return false
	return true

func _post_run(run: PackedInt32Array, chain_id: int) -> void:
	var w: M7WorldData = b.w
	var n := run.size()
	if n < MIN_RUN:
		return
	var at := END_MARGIN
	var index := 0
	while at <= n - 1 - END_MARGIN:
		# the highest usable tile in the next stretch of the trail
		var best := -1
		var best_h := -1e9
		for i in range(at, mini(at + SPACING, n - END_MARGIN)):
			var tile := run[i]
			var h: float = w.heights[tile]
			var c: Vector2 = w.tile_center(tile)
			if h > best_h and _post_ok(tile, c):
				best = i
				best_h = h
		if best < 0:
			at += SPACING
			continue
		var tile := run[best]
		var seed := M7WorldData.hash01(tile, chain_id, 51)
		var c: Vector2 = w.tile_center(tile)
		var p := c + Vector2(seed - 0.5, M7WorldData.hash01(tile, chain_id, 52) - 0.5) * 0.3
		if not _post_ok(tile, p):
			p = c
		_make_post(p, chain_id, index, seed)
		index += 1
		at = best + SPACING

func _make_post(p: Vector2, chain_id: int, index: int, seed: float) -> void:
	var w: M7WorldData = b.w
	var y := maxf(w.height_at(p), 0.0)
	var mound := MeshInstance3D.new()
	mound.mesh = b.kit.get_mesh("decor_signal")
	mound.material_override = b.mats.prop
	mound.transform = Transform3D(Basis(Vector3.UP, seed * TAU).scaled(Vector3.ONE * POST_SCALE), Vector3(p.x, y, p.y))
	holder.add_child(mound)
	var flame := MeshInstance3D.new()
	flame.mesh = b.kit.get_mesh("flame")
	flame.material_override = b.mats.fire
	flame.position = Vector3(p.x, y + 0.075 * POST_SCALE, p.y)
	holder.add_child(flame)
	var light := OmniLight3D.new()
	light.light_color = Color("#ff9440")
	light.omni_range = 0.9
	light.omni_attenuation = 1.5
	light.shadow_enabled = false
	light.position = Vector3(p.x, y + 0.14 * POST_SCALE, p.y)
	holder.add_child(light)
	var smoke: GPUParticles3D = smoke_factory.call()
	# a thin, tall thread of smoke that reads from far away
	var pm := (smoke.process_material as ParticleProcessMaterial).duplicate() as ParticleProcessMaterial
	pm.initial_velocity_min = 0.16
	pm.initial_velocity_max = 0.22
	pm.spread = 4.0
	pm.scale_min = 1.6
	pm.scale_max = 2.4
	# darker and denser than a hearth's, so it reads as a column from far off
	var ramp := Gradient.new()
	ramp.set_color(0, Color(0.42, 0.40, 0.38, 0.0))
	ramp.set_color(1, Color(0.5, 0.48, 0.46, 0.0))
	ramp.add_point(0.1, Color(0.36, 0.34, 0.33, 0.9))
	ramp.add_point(0.55, Color(0.45, 0.43, 0.42, 0.6))
	var ramp_texture := GradientTexture1D.new()
	ramp_texture.gradient = ramp
	pm.color_ramp = ramp_texture
	smoke.process_material = pm
	smoke.lifetime = 9.0
	smoke.preprocess = 9.0
	smoke.amount = 40
	(smoke.draw_pass_1 as QuadMesh).size = Vector2(0.2, 0.2)
	smoke.visibility_aabb = AABB(Vector3(-2, -0.5, -2), Vector3(4, 5, 4))
	smoke.position = Vector3(p.x, y + 0.13 * POST_SCALE, p.y)
	holder.add_child(smoke)
	posts.append({"pos": p, "chain": chain_id, "index": index, "flame": flame, "light": light, "smoke": smoke, "seed": seed})

## 0 resting .. 1 flared, for the post at `index` in its chain: a bell that runs down the chain once every PERIOD seconds.
static func flare_at(time: float, chain_id: int, index: int) -> float:
	var phase := fposmod(time - index * STEP - chain_id * 2.3, PERIOD)
	if phase >= FLARE:
		return 0.0
	return sin(PI * phase / FLARE)

func update(time: float) -> void:
	for post in posts:
		var f := flare_at(time, post.chain, post.index)
		var flame: MeshInstance3D = post.flame
		var s := 0.55 + 0.9 * f
		flame.scale = Vector3(s, s * (0.9 + 0.5 * f), s)
		var light: OmniLight3D = post.light
		light.light_energy = 0.35 + 1.5 * f
		var smoke: GPUParticles3D = post.smoke
		smoke.amount_ratio = 0.35 + 0.65 * f
