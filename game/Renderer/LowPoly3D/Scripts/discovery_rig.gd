## What moves in the Discovery age's works: the coal mine's winding wheel (MIN-09), the atmospheric engine's rocking beam and its steam (NRG-06), and the smoke
## over the factory (MEC-10), the engine house, the coke ovens (FIR-12) and the mine's winding house, docs/MVP/39 §4.1 to §4.4 and §8.1.
##
## The still pieces are the meshes of art_disc_power.gd, laid out with the village; this rig only adds what moves, as the war rig does for the Iron forge. It is
## given a list of works, each {kind, pos, yaw, scale, state} (kind: coal_mine, atmospheric_engine, factory, coke_oven; state: work, idle, flooded or cold), and
## animates only those whose state is `work`: an idle, flooded or cold works stands still and gives no smoke (docs/MVP/39 §8.1: an engine with no coal or staff
## must look cold). Everything is a pure function of the time handed to `update`, so pause, speed and loading all work and nothing is stored.
##
## Not wired to the simulation yet (the frame's `discWorks` and `discWorkState`, docs/MVP/39 §9, are what will feed `sync`); the Discovery gallery feeds it a demo list
## (LowPoly3DView.show_discovery_gallery) and drives it from the wall clock so the motion can be photographed on a paused match.
##
## Smoke budget (docs/MVP/39 §12): at most one smoke column per working industrial building (the engine adds one small steam puff, the mine's winding house a
## thin thread), and at most MAX_COLUMNS columns in the whole scene however many works there are; works beyond that animate their parts but are silent.
class_name M7DiscoveryRig
extends RefCounted

const MAX_COLUMNS := 24
const WHEEL_SPEED := 1.4           # radians per second of the winding wheel
const BEAM_PERIOD := 4.0           # seconds per rock of the engine's beam
const BEAM_SWING := 0.15           # radians either side of level
const Power := preload("res://Renderer/LowPoly3D/Scripts/art_disc_power.gd")   # where each thing sits in its mesh's own frame is defined there, once

var b                              # the M7WorldBuilder
var parent: Node3D
var smoke_factory: Callable
var holder: Node3D
var wheels: Array = []             # {node, base, hub, rot, spin}
var beams: Array = []              # {node, pivot, rot, scale, phase}
var columns: Array = []            # {smoke, work}
var soot_material: ShaderMaterial  # the shared smoke shader with its colour set near-black (it ignores a particle's own colour)
var steam_material: ShaderMaterial # and the same set white
var dust_material: ShaderMaterial  # and a pale brown for the dust of a wall under fire
var demo := false                  # the gallery drives the rig from the wall clock, the game from the render tick
var signature := -1

func _init(builder, parent_node: Node3D, smoke_maker: Callable) -> void:
	b = builder
	parent = parent_node
	smoke_factory = smoke_maker
	soot_material = _tinted(Color(0.2, 0.19, 0.19))
	steam_material = _tinted(Color(0.97, 0.98, 1.0))
	dust_material = _tinted(Color(0.68, 0.6, 0.48))

## A copy of the world's smoke material with its colour changed; the shared shader (smoke.gdshader) is left as it is, so no older render changes.
func _tinted(col: Color) -> ShaderMaterial:
	var m := (b.mats.smoke as ShaderMaterial).duplicate() as ShaderMaterial
	m.set_shader_parameter("smoke_col", col)
	return m

## A cool, thin, quick white puff for steam (the shared smoke is grey and slow): a short lifetime and a pale ramp.
static func steam(smoke: GPUParticles3D, lift: float, amount: int) -> void:
	M7SignalRig.thicken_smoke(smoke, lift, amount, 2.4)
	var pm := (smoke.process_material as ParticleProcessMaterial).duplicate() as ParticleProcessMaterial
	var ramp := Gradient.new()
	ramp.set_color(0, Color(0.96, 0.97, 1.0, 0.0))
	ramp.set_color(1, Color(0.96, 0.97, 1.0, 0.0))
	ramp.add_point(0.12, Color(0.96, 0.97, 1.0, 0.8))
	ramp.add_point(0.6, Color(0.94, 0.95, 0.98, 0.4))
	var tex := GradientTexture1D.new()
	tex.gradient = ramp
	pm.color_ramp = tex
	pm.scale_min = 0.8
	pm.scale_max = 1.3
	smoke.process_material = pm
	(smoke.draw_pass_1 as QuadMesh).size = Vector2(0.13, 0.13)

## Coal and factory smoke: the shared column's shape but near-black, so an industrial chimney reads as one from a distance (docs/MVP/39 §8.1, the COAL_SMOKE tone).
static func soot(smoke: GPUParticles3D, lift: float, amount: int, lifetime: float) -> void:
	M7SignalRig.thicken_smoke(smoke, lift, amount, lifetime)
	var pm := (smoke.process_material as ParticleProcessMaterial).duplicate() as ParticleProcessMaterial
	var ramp := Gradient.new()
	ramp.set_color(0, Color(0.1, 0.1, 0.1, 0.0))
	ramp.set_color(1, Color(0.2, 0.2, 0.21, 0.0))
	ramp.add_point(0.1, Color(0.08, 0.08, 0.08, 0.95))
	ramp.add_point(0.55, Color(0.16, 0.16, 0.17, 0.7))
	var tex := GradientTexture1D.new()
	tex.gradient = ramp
	pm.color_ramp = tex
	smoke.process_material = pm

func _clear() -> void:
	if holder != null:
		holder.queue_free()
	holder = Node3D.new()
	holder.name = "DiscoveryWorks"
	parent.add_child(holder)
	wheels.clear()
	beams.clear()
	columns.clear()

## Rebuilds the moving parts for a list of works. `works` entries: {kind, pos: Vector3, yaw: float, scale: float, state: String}.
func sync(works: Array) -> void:
	var sig := hash(works)             # every field of every work, so any change rebuilds
	if sig == signature:
		return
	signature = sig
	_clear()
	if not works.is_empty():
		b.kit.ensure_discovery()
	for w in works:
		var working := String(w.state) == "work"
		var rot := M7WorldBuilder.rot_y(float(w.get("yaw", 0.0)))
		var scale := float(w.get("scale", 1.0))
		var pos: Vector3 = w.pos
		match String(w.kind):
			"coal_mine":
				if working:
					var wheel := MeshInstance3D.new()
					wheel.mesh = b.kit.get_mesh("coal_mine_wheel")
					wheel.material_override = b.mats.prop
					wheel.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
					holder.add_child(wheel)
					wheels.append({"node": wheel, "pos": pos, "rot": rot, "scale": scale, "hub": Power.WHEEL_HUB, "spin": 1.0})
					_column(pos + rot * (Power.MINE_SMOKE * scale), 0.05, 6, 4.0, false, scale, false)
			"atmospheric_engine":
				if working:
					var beam := MeshInstance3D.new()
					beam.mesh = b.kit.get_mesh("atmospheric_engine_beam")
					beam.material_override = b.mats.prop
					beam.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
					holder.add_child(beam)
					beams.append({"node": beam, "pos": pos, "rot": rot, "scale": scale, "pivot": Power.BEAM_PIVOT, "phase": float(absi(String(w.get("seed", 0)).hash()) % 100) / 100.0})
					_column(pos + rot * (Power.ENGINE_SMOKE * scale), 0.07, 14, 5.0, false, scale)
					_column(pos + rot * (Power.ENGINE_VALVE * scale), 0.05, 8, 3.0, true, scale)
			"factory":
				if working:
					_column(pos + rot * (Power.FACTORY_SMOKE * scale), 0.09, 24, 6.5, false, scale)
			"bastion_fort":
				# a fort under fire: the dust of fresh hits over the back bastion while it is cracked or breached (a ruin has stopped being fired at)
				if String(w.state) == "cracked" or String(w.state) == "breach":
					_column(pos + rot * (Vector3(0.0, 0.05, -0.33) * scale), 0.03, 7, 2.4, false, scale, true, dust_material)
			"coke_oven":
				if working:
					_column(pos + rot * (Power.COKE_SMOKE * scale), 0.08, 18, 6.0, false, scale)

func _column(at: Vector3, lift: float, amount: int, lifetime: float, is_steam: bool, scale: float, dark: bool = true, material: ShaderMaterial = null) -> void:
	if columns.size() >= MAX_COLUMNS:
		return
	var smoke: GPUParticles3D = smoke_factory.call()
	if is_steam:
		steam(smoke, lift * scale, amount)
		(smoke.draw_pass_1 as QuadMesh).material = steam_material
	elif dark:
		soot(smoke, lift * scale, amount, lifetime)
		(smoke.draw_pass_1 as QuadMesh).material = material if material != null else soot_material
		if material == dust_material:
			(smoke.draw_pass_1 as QuadMesh).size = Vector2(0.1, 0.1)       # dust is a small cloud, not a column
	else:
		M7SignalRig.thicken_smoke(smoke, lift * scale, amount, lifetime)
	smoke.position = at
	holder.add_child(smoke)
	columns.append({"smoke": smoke})

## Turns the wheels and rocks the beams for `seconds` of render time (the render tick * 0.1 in a match, the wall clock in the gallery).
func update(seconds: float) -> void:
	for wh in wheels:
		var rot: Basis = wh.rot
		var s: float = wh.scale
		var angle := seconds * WHEEL_SPEED * float(wh.spin)
		(wh.node as MeshInstance3D).transform = Transform3D(rot * Basis.from_scale(Vector3.ONE * s) * Basis(Vector3.BACK, angle), (wh.pos as Vector3) + rot * ((wh.hub as Vector3) * s))
	for bm in beams:
		var rot2: Basis = bm.rot
		var s2: float = bm.scale
		var angle2 := BEAM_SWING * sin(TAU * (seconds / BEAM_PERIOD + float(bm.phase)))
		(bm.node as MeshInstance3D).transform = Transform3D(rot2 * Basis.from_scale(Vector3.ONE * s2) * Basis(Vector3.BACK, angle2), (bm.pos as Vector3) + rot2 * ((bm.pivot as Vector3) * s2))
