## Tabletop orbit camera: drag to rotate (left) or tilt (right / shift-drag), wheel to zoom, WASD / arrows to pan.
extends Camera3D

var target := Vector3(48, 0.2, 30)
var distance := 30.0
var yaw := 0.0 # radians, 0 = looking north
var pitch := deg_to_rad(48.0)
var min_distance := 2.0
var max_distance := 160.0
var ground_fn: Callable # Vector2 -> float, keeps the target on the ground

var _drag := false
var _tilt := false


func _ready() -> void:
	fov = 30.0
	near = 0.05
	far = 900.0
	apply()


func set_view(t: Vector3, dist: float, yaw_deg: float, pitch_deg: float) -> void:
	target = t
	distance = dist
	yaw = deg_to_rad(yaw_deg)
	pitch = deg_to_rad(pitch_deg)
	apply()


func apply() -> void:
	var dir := Vector3(sin(yaw) * cos(pitch), sin(pitch), cos(yaw) * cos(pitch))
	global_position = target + dir * distance
	look_at(target, Vector3.UP)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_WHEEL_UP and mb.pressed:
			distance = maxf(min_distance, distance * 0.88)
			apply()
		elif mb.button_index == MOUSE_BUTTON_WHEEL_DOWN and mb.pressed:
			distance = minf(max_distance, distance / 0.88)
			apply()
		elif mb.button_index == MOUSE_BUTTON_LEFT:
			_drag = mb.pressed
			_tilt = mb.shift_pressed
		elif mb.button_index == MOUSE_BUTTON_RIGHT:
			_drag = mb.pressed
			_tilt = true
	elif event is InputEventMouseMotion and _drag:
		var mm := event as InputEventMouseMotion
		if _tilt:
			pitch = clampf(pitch + mm.relative.y * 0.005, deg_to_rad(20), deg_to_rad(88))
		else:
			yaw -= mm.relative.x * 0.006
			pitch = clampf(pitch + mm.relative.y * 0.004, deg_to_rad(20), deg_to_rad(88))
		apply()


func _process(delta: float) -> void:
	var v := Vector2.ZERO
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP):
		v.y -= 1
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN):
		v.y += 1
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT):
		v.x -= 1
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT):
		v.x += 1
	if Input.is_key_pressed(KEY_Q):
		yaw += delta * 1.2
	if Input.is_key_pressed(KEY_E):
		yaw -= delta * 1.2
	if v != Vector2.ZERO or Input.is_key_pressed(KEY_Q) or Input.is_key_pressed(KEY_E):
		var speed := distance * 0.9 * delta
		var fwd := Vector3(-sin(yaw), 0, -cos(yaw))
		var right := Vector3(cos(yaw), 0, -sin(yaw))
		target += (right * v.x - fwd * v.y) * speed
		if ground_fn.is_valid():
			target.y = ground_fn.call(Vector2(target.x, target.z))
		apply()
