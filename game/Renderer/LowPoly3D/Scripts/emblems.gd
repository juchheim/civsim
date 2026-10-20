## Civ emblems (docs/MVP/08 §4.3) as simple polygons in [-1,1]^2, y up.
## sun, wave, leaf, star, mountain, flame, moon, antler.
class_name M7Emblems
extends RefCounted

static func polygons(civ: int) -> Array:
	match civ % 8:
		0: return _sun()
		1: return _wave()
		2: return [_leaf()]
		3: return [_star()]
		4: return [_mountain()]
		5: return [_flame()]
		6: return [_moon()]
		_: return _antler()

static func _circle(c: Vector2, r: float, n: int) -> PackedVector2Array:
	var p := PackedVector2Array()
	for i in n:
		var a := -TAU * i / n
		p.append(c + Vector2(cos(a), sin(a)) * r)
	return p

static func _sun() -> Array:
	var polys := [_circle(Vector2.ZERO, 0.5, 12)]
	for i in 8:
		var a := TAU * i / 8.0
		var d := Vector2(cos(a), sin(a))
		var s := Vector2(-d.y, d.x)
		polys.append(PackedVector2Array([d * 0.62 + s * 0.13, d * 1.0, d * 0.62 - s * 0.13]))
	return polys

static func _wave() -> Array:
	var polys := []
	for row in [-0.35, 0.35]:
		var top := PackedVector2Array()
		var bot := PackedVector2Array()
		for i in 13:
			var x := -1.0 + 2.0 * i / 12.0
			var y: float = row + 0.25 * sin(x * PI * 1.5)
			top.append(Vector2(x, y + 0.13))
			bot.append(Vector2(x, y - 0.13))
		bot.reverse()
		top.append_array(bot)
		polys.append(top)
	return polys

static func _leaf() -> PackedVector2Array:
	var p := PackedVector2Array()
	for i in 9:
		var t := float(i) / 8.0
		var a := lerpf(-PI * 0.5, PI * 0.5, t)
		p.append(Vector2(sin(a) * 0.95, cos(a) * 0.5).rotated(0.6))
	for i in range(1, 8):
		var t := float(i) / 8.0
		var a := lerpf(PI * 0.5, -PI * 0.5, t)
		p.append(Vector2(sin(a) * 0.95, -cos(a) * 0.5).rotated(0.6))
	return p

static func _star() -> PackedVector2Array:
	var p := PackedVector2Array()
	for i in 10:
		var a := PI * 0.5 - PI * i / 5.0
		var r := 1.0 if i % 2 == 0 else 0.42
		p.append(Vector2(cos(a), sin(a)) * r)
	return p

static func _mountain() -> PackedVector2Array:
	return PackedVector2Array([Vector2(-1.0, -0.7), Vector2(-0.35, 0.55), Vector2(-0.1, 0.15), Vector2(0.3, 0.85),
		Vector2(1.0, -0.7)])

static func _flame() -> PackedVector2Array:
	var p := PackedVector2Array()
	for i in 16:
		var a := TAU * i / 16.0
		var r := 0.55
		var v := Vector2(sin(a) * r, -cos(a) * r - 0.25)
		if v.y > -0.25:
			# taper the top into a point that leans right
			var k := (v.y + 0.25) / 0.55
			v.x *= 1.0 - 0.75 * k
			v.y += k * 0.75
			v.x += k * 0.18
		p.append(v)
	return p

static func _moon() -> PackedVector2Array:
	var p := PackedVector2Array()
	for i in 13:
		var a := lerpf(PI * 0.35, PI * 1.65, float(i) / 12.0)
		p.append(Vector2(cos(a), sin(a)) * 0.9)
	for i in 13:
		var a := lerpf(PI * 1.5, PI * 0.5, float(i) / 12.0)
		p.append(Vector2(cos(a) * 0.45 - 0.15, sin(a) * 0.72))
	return p

static func _bar(a: Vector2, b: Vector2, w: float) -> PackedVector2Array:
	var d := (b - a).normalized()
	var s := Vector2(-d.y, d.x) * w
	return PackedVector2Array([a + s, b + s, b - s, a - s])

static func _antler() -> Array:
	return [
		_bar(Vector2(0, -0.9), Vector2(-0.2, 0.9), 0.1),
		_bar(Vector2(-0.08, -0.15), Vector2(0.55, 0.25), 0.09),
		_bar(Vector2(-0.14, 0.35), Vector2(0.45, 0.85), 0.09),
		_bar(Vector2(-0.04, -0.5), Vector2(-0.7, -0.15), 0.09),
	]
