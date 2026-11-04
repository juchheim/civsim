## What moves in the Iron Age's forges and sieges: the flame, glow and smoke over a forge that is at work (MET-09), and the throwing arms and shot of the
## siege engines (WAR-09) that stand round a besieged town while a wall's integrity falls.
##
## The static pieces (the forge yard, the engine frames, tents, the ruined wall) are props laid out with the village (world_builder.gd, `forge_sites` and
## `siege_engines`); this rig only adds what moves. Everything is a pure function of the render tick, so pause, speed and loading all work and nothing is
## stored. The throwing is cosmetic: it shows the siege, it does not decide it.
class_name M7WarRig
extends RefCounted

const PERIOD := 90.0               # ticks between shots of one engine (9 seconds at 1x)
const WIND_END := 0.6              # fractions of the period: winding the arm down, a pause cocked, the swing, the arm hanging up
const PAUSE_END := 0.7
const SWING_END := 0.78
const FLIGHT_END := 0.93
const ARM_COCKED := 1.0            # radians about X: the long end down
const ARM_THROWN := -1.2           # and up
const ES := 1.2                    # engines and yards are drawn larger than life, like the rest of the village

var b                              # the M7WorldBuilder
var parent: Node3D
var smoke_factory: Callable
var holder: Node3D
var forges: Array = []             # {flame, light, smoke, seed}
var engines: Array = []            # {arm, stone, site}
var signature := -1

func _init(builder, parent_node: Node3D, smoke_maker: Callable) -> void:
	b = builder
	parent = parent_node
	smoke_factory = smoke_maker

static func _smooth(x: float) -> float:
	var t := clampf(x, 0.0, 1.0)
	return t * t * (3.0 - 2.0 * t)

## Rebuilds the effects when a forge lights, goes out or moves, or an engine train arrives, changes or leaves.
func sync() -> void:
	var sig := 17
	for f in b.forge_sites:
		sig = sig * 31 + int(float((f.chimney as Vector3).x) * 1000.0) + int(float((f.chimney as Vector3).z) * 777.0)
	for e in b.siege_engines:
		sig = sig * 31 + int(float((e.p as Vector3).x) * 1000.0) + int(float((e.p as Vector3).z) * 777.0) + int(e.k)
	if sig == signature:
		return
	signature = sig
	if holder != null:
		holder.queue_free()
	holder = Node3D.new()
	holder.name = "War"
	parent.add_child(holder)
	forges.clear()
	engines.clear()
	for f in b.forge_sites:
		var flame := MeshInstance3D.new()
		flame.mesh = b.kit.get_mesh("flame")
		flame.material_override = b.mats.fire
		flame.position = f.mouth
		holder.add_child(flame)
		var light := OmniLight3D.new()
		light.light_color = Color("#ff8a30")
		light.omni_range = 0.7
		light.omni_attenuation = 1.5
		light.shadow_enabled = false
		light.position = (f.mouth as Vector3) + Vector3(0, 0.04, 0.04)
		holder.add_child(light)
		var smoke: GPUParticles3D = smoke_factory.call()
		M7SignalRig.thicken_smoke(smoke, 0.1, 18, 6.0)
		smoke.position = f.chimney
		holder.add_child(smoke)
		forges.append({"flame": flame, "light": light, "smoke": smoke, "seed": float(f.seed)})
	for e in b.siege_engines:
		var arm := MeshInstance3D.new()
		arm.mesh = b.kit.get_mesh("siege_arm")
		arm.material_override = b.mats.prop
		arm.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
		holder.add_child(arm)
		var stone := MeshInstance3D.new()
		stone.mesh = b.kit.get_mesh("siege_stone")
		stone.material_override = b.mats.prop
		stone.visible = false
		holder.add_child(stone)
		engines.append({"arm": arm, "stone": stone, "site": e})

func update(tick: float) -> void:
	var seconds := tick * 0.1
	for f in forges:
		var flick := 0.8 + 0.2 * sin(seconds * 9.0 + float(f.seed) * 11.0) + 0.08 * sin(seconds * 23.0)
		var fs := 0.34 * flick
		(f.flame as MeshInstance3D).scale = Vector3(fs, fs * 1.1, fs)
		(f.light as OmniLight3D).light_energy = 0.9 * flick
	for e in engines:
		_play_engine(e, tick)

func _play_engine(e: Dictionary, tick: float) -> void:
	var site: Dictionary = e.site
	var phase := fposmod(tick / PERIOD + float(site.seed), 1.0)
	var a := ARM_THROWN
	if phase < WIND_END:
		a = lerpf(ARM_THROWN, ARM_COCKED, _smooth(phase / WIND_END))
	elif phase < PAUSE_END:
		a = ARM_COCKED
	elif phase < SWING_END:
		a = lerpf(ARM_COCKED, ARM_THROWN, _smooth((phase - PAUSE_END) / (SWING_END - PAUSE_END)))
	var yaw := float(site.yaw)
	var base: Vector3 = site.p
	var rot := M7WorldBuilder.rot_y(yaw)
	var pivot := base + rot * Vector3(0, 0.2 * ES, 0)
	(e.arm as MeshInstance3D).transform = Transform3D(rot * Basis(Vector3.RIGHT, a) * Basis.from_scale(Vector3.ONE * ES), pivot)
	var stone := e.stone as MeshInstance3D
	if phase >= PAUSE_END + (SWING_END - PAUSE_END) * 0.5 and phase < FLIGHT_END:
		var t := (phase - (PAUSE_END + (SWING_END - PAUSE_END) * 0.5)) / (FLIGHT_END - (PAUSE_END + (SWING_END - PAUSE_END) * 0.5))
		var start := pivot + rot * Vector3(0, 0.12 * ES, 0.3 * ES)
		var target: Vector3 = site.target
		stone.position = start.lerp(target, t) + Vector3(0, sin(PI * t) * 0.5, 0)
		stone.scale = Vector3.ONE * ES
		stone.visible = true
	else:
		stone.visible = false
