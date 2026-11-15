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
# the cannon (WAR-12): one shot every CANNON_PERIOD ticks, fired at CANNON_FIRE of the period
const CANNON_PERIOD := 110.0
const CANNON_FIRE := 0.78
const CANNON_MUZZLE := Vector3(0.0, 0.046, 0.067)    # the muzzle's place in the cannon's own frame (art_fief_war.gd), before the scale ES
const RECOIL := 0.05                                  # how far the carriage runs back, in its own length
const BALL_FLIGHT := 0.3                              # seconds the shot takes to the wall
const SMOKE_PUFFS := 3
const ES := 1.2                    # engines and yards are drawn larger than life, like the rest of the village

var b                              # the M7WorldBuilder
var parent: Node3D
var smoke_factory: Callable
var holder: Node3D
var forges: Array = []             # {flame, light, smoke, seed}
var engines: Array = []            # {arm, stone, site}
var cannons: Array = []            # {body, flash, light, ball, smoke[], dust[], site}
var beacons: Array = []
var chimneys: Array = []           # {smoke}: bathhouse chimneys            # {flame, light, smoke, seed}: the Great Lighthouse's fire
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
	for bf in b.beacon_fires:
		sig = sig * 31 + int(float((bf.top as Vector3).x) * 1000.0) + int(float((bf.top as Vector3).z) * 777.0)
	for ch in b.chimney_smokes:
		sig = sig * 31 + int(float((ch.pos as Vector3).x) * 1000.0) + int(float((ch.pos as Vector3).z) * 777.0)
	for e in b.siege_engines:
		sig = sig * 31 + int(float((e.p as Vector3).x) * 1000.0) + int(float((e.p as Vector3).z) * 777.0) + int(e.k)
	for cn in b.siege_cannons:
		sig = sig * 31 + int(float((cn.p as Vector3).x) * 1000.0) + int(float((cn.p as Vector3).z) * 777.0) + 5
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
	beacons.clear()
	chimneys.clear()
	cannons.clear()
	for ch in b.chimney_smokes:
		var puff: GPUParticles3D = smoke_factory.call()
		M7SignalRig.thicken_smoke(puff, 0.07, 10, 5.0)
		puff.amount_ratio = 0.7
		puff.position = ch.pos
		holder.add_child(puff)
		chimneys.append({"smoke": puff})
	for bf in b.beacon_fires:
		var flame := MeshInstance3D.new()
		flame.mesh = b.kit.get_mesh("flame")
		flame.material_override = b.mats.fire
		flame.position = bf.top
		holder.add_child(flame)
		var light := OmniLight3D.new()
		light.light_color = Color("#ffb04a")
		light.omni_range = 2.2
		light.omni_attenuation = 1.3
		light.shadow_enabled = false
		light.position = (bf.top as Vector3) + Vector3(0, 0.04, 0)
		holder.add_child(light)
		var smoke: GPUParticles3D = smoke_factory.call()
		M7SignalRig.thicken_smoke(smoke, 0.1, 14, 7.0)
		smoke.position = (bf.top as Vector3) + Vector3(0, 0.06, 0)
		holder.add_child(smoke)
		beacons.append({"flame": flame, "light": light, "smoke": smoke, "seed": float(bf.seed)})
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
	for cn in b.siege_cannons:
		cannons.append(_make_cannon(cn))

func update(tick: float) -> void:
	var seconds := tick * 0.1
	for f in forges:
		var flick := 0.8 + 0.2 * sin(seconds * 9.0 + float(f.seed) * 11.0) + 0.08 * sin(seconds * 23.0)
		var fs := 0.34 * flick
		(f.flame as MeshInstance3D).scale = Vector3(fs, fs * 1.1, fs)
		(f.light as OmniLight3D).light_energy = 0.9 * flick
	for bc in beacons:
		var flick2 := 0.85 + 0.15 * sin(seconds * 7.0 + float(bc.seed) * 13.0) + 0.06 * sin(seconds * 19.0)
		var fs2 := 0.7 * flick2
		(bc.flame as MeshInstance3D).scale = Vector3(fs2, fs2 * 1.15, fs2)
		(bc.light as OmniLight3D).light_energy = 1.5 * flick2
	for e in engines:
		_play_engine(e, tick)
	for c in cannons:
		_play_cannon(c, tick)

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


## A translucent grey puff, one material of its own so its fade is its own.
func _puff_node(colour: Color) -> MeshInstance3D:
	var m := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 0.5
	sphere.height = 1.0
	sphere.radial_segments = 7
	sphere.rings = 4
	m.mesh = sphere
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.albedo_color = colour
	m.material_override = mat
	m.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	m.visible = false
	holder.add_child(m)
	return m

func _make_cannon(site: Dictionary) -> Dictionary:
	var body := MeshInstance3D.new()
	body.mesh = b.kit.get_mesh("cannon")
	body.material_override = b.mats.prop
	body.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	holder.add_child(body)
	var flash := MeshInstance3D.new()
	flash.mesh = b.kit.get_mesh("flame")
	flash.material_override = b.mats.fire
	flash.visible = false
	holder.add_child(flash)
	var light := OmniLight3D.new()
	light.light_color = Color("#ffb04a")
	light.omni_range = 0.45
	light.omni_attenuation = 1.6
	light.shadow_enabled = false
	light.light_energy = 0.0
	holder.add_child(light)
	var ball := MeshInstance3D.new()
	var sph := SphereMesh.new()
	sph.radius = 0.008
	sph.height = 0.016
	sph.radial_segments = 6
	sph.rings = 3
	ball.mesh = sph
	var iron := StandardMaterial3D.new()
	iron.albedo_color = Color("#2a2e34")
	iron.roughness = 0.6
	ball.material_override = iron
	ball.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	ball.visible = false
	holder.add_child(ball)
	var smoke := []
	for i in SMOKE_PUFFS:
		smoke.append(_puff_node(Color(0.5, 0.49, 0.47, 0.6)))
	var dust := []
	for i in 2:
		dust.append(_puff_node(Color(0.62, 0.55, 0.45, 0.6)))
	return {"body": body, "flash": flash, "light": light, "ball": ball, "smoke": smoke, "dust": dust, "site": site}

## One cannon's cycle, a pure function of the render tick (so pause and speed work): it stands, fires, runs back on its carriage and rolls forward again; a flash and a
## flicker of light at the muzzle, three puffs of smoke drifting off, the shot flying to the wall, and dust where it lands.
func _play_cannon(c: Dictionary, tick: float) -> void:
	var site: Dictionary = c.site
	var seconds := tick * 0.1
	var period := CANNON_PERIOD * 0.1
	var cycle := fposmod(seconds + float(site.seed) * period, period)
	var t := cycle - CANNON_FIRE * period          # seconds since the shot, negative before it
	var yaw := float(site.yaw)
	var base: Vector3 = site.p
	var rot := M7WorldBuilder.rot_y(yaw)
	# recoil: out fast, then rolling home
	var back := 0.0
	if t >= 0.0:
		back = _smooth(t / 0.08) if t < 0.08 else 1.0 - _smooth((t - 0.08) / 1.1)
	var origin := base - rot * Vector3(0.0, 0.0, RECOIL * ES * back)
	(c.body as MeshInstance3D).transform = Transform3D(rot * Basis.from_scale(Vector3.ONE * ES), origin)
	var muzzle: Vector3 = origin + rot * (CANNON_MUZZLE * ES)
	# flash and light
	var flash := c.flash as MeshInstance3D
	var light := c.light as OmniLight3D
	if t >= 0.0 and t < 0.12:
		var k := 1.0 - t / 0.12
		flash.visible = true
		flash.position = muzzle + rot * Vector3(0.0, 0.0, 0.02)
		flash.scale = Vector3.ONE * (0.5 * (0.4 + 0.6 * k))
		light.position = muzzle + Vector3(0, 0.03, 0)
		light.light_energy = 0.55 * k
	else:
		flash.visible = false
		light.light_energy = 0.0
	# smoke: puffs that swell and thin as they drift off along the barrel and up
	var smoke: Array = c.smoke
	for i in smoke.size():
		var node := smoke[i] as MeshInstance3D
		var u := (t - 0.03 * i) / 1.7
		if u < 0.0 or u > 1.0:
			node.visible = false
			continue
		var size := lerpf(0.02, 0.085, 1.0 - pow(1.0 - u, 2.0)) * ES
		node.position = muzzle + rot * Vector3((i - 1) * 0.012, 0.0, 0.03 + 0.07 * u) + Vector3(0, 0.05 * u, 0)
		node.scale = Vector3.ONE * size
		(node.material_override as StandardMaterial3D).albedo_color = Color(0.52, 0.5, 0.48, 0.6 * (1.0 - _smooth(u)) * minf(1.0, u * 12.0))
		node.visible = true
	# the shot: a dark ball on a shallow arc from the muzzle to the wall
	var ball := c.ball as MeshInstance3D
	var target: Vector3 = site.target
	var f := (t - 0.02) / BALL_FLIGHT
	if f >= 0.0 and f < 1.0:
		ball.position = muzzle.lerp(target, f) + Vector3(0, sin(PI * f) * 0.03, 0)
		ball.scale = Vector3.ONE * ES
		ball.visible = true
	else:
		ball.visible = false
	# the hit: dust jumping off the wall where it lands
	var dust: Array = c.dust
	for i in dust.size():
		var node := dust[i] as MeshInstance3D
		var u := (t - 0.02 - BALL_FLIGHT - 0.05 * i) / 1.1
		if u < 0.0 or u > 1.0:
			node.visible = false
			continue
		var size := lerpf(0.03, 0.11, 1.0 - pow(1.0 - u, 2.0)) * ES
		node.position = target + Vector3((i - 0.5) * 0.04, 0.02 + 0.06 * u, 0.0)
		node.scale = Vector3.ONE * size
		(node.material_override as StandardMaterial3D).albedo_color = Color(0.62, 0.55, 0.46, 0.55 * (1.0 - _smooth(u)) * minf(1.0, u * 15.0))
		node.visible = true
