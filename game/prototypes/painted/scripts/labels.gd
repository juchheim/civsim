## Parchment ribbon labels for settlements, drawn as a 2D overlay projected from the world so the
## lettering stays crisp at every zoom. Each label carries the civ emblem on a civ-coloured seal.
extends Control

const Palette = preload("res://scripts/palette.gd")

const SYL := [
	["Ka", "ru", "sa", "na", "to", "mi", "ra", "en", "ko", "ha"],
	["Ve", "li", "sor", "an", "ith", "el", "mar", "wen", "ys", "ta"],
	["Ob", "u", "ga", "lo", "nde", "ma", "zu", "ba", "ri", "ko"],
	["Thal", "dr", "or", "eth", "ka", "vin", "ul", "gar", "ra", "ost"],
]

var cam: Camera3D
var items := [] # {pos: Vector3, name, civ, pop, big}
var font: Font
var emblem_scale := 1.0
var visible_dist := 260.0


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_preset(Control.PRESET_FULL_RECT)
	var sf := SystemFont.new()
	sf.font_names = PackedStringArray(["Palatino", "Book Antiqua", "Georgia", "Times New Roman", "serif"])
	sf.font_weight = 600
	sf.antialiasing = TextServer.FONT_ANTIALIASING_GRAY
	font = sf


static func make_name(civ: int, i: int) -> String:
	var syl: Array = SYL[civ % SYL.size()]
	var h := func(a: int, b: int) -> int: return absi(int(sin(a * 12.9898 + b * 78.233) * 43758.5453)) % 1000
	var n: int = 2 + h.call(i, civ) % 2
	var s := ""
	for k in n:
		s += syl[h.call(i * 7 + k, civ * 3 + k) % syl.size()]
	s = s.to_lower()
	return s.substr(0, 1).to_upper() + s.substr(1)


func set_settlements(fd, bake) -> void:
	items.clear()
	var best := {}
	for i in fd.settlements.size():
		var s: Dictionary = fd.settlements[i]
		if not best.has(s.civ) or s.population > fd.settlements[best[s.civ]].population:
			best[s.civ] = i
	for i in fd.settlements.size():
		var s: Dictionary = fd.settlements[i]
		var p: Vector2 = s.pos
		items.append({"pos": Vector3(p.x, bake.height_at(p) + 0.42, p.y), "name": make_name(s.civ, i),
			"civ": s.civ, "pop": s.population, "big": best[s.civ] == i})


func _process(_d: float) -> void:
	queue_redraw()


func _draw() -> void:
	if cam == null:
		return
	var order := []
	for it in items:
		if cam.is_position_behind(it.pos):
			continue
		var dist := cam.global_position.distance_to(it.pos)
		if dist > visible_dist:
			continue
		order.append([dist, it])
	order.sort_custom(func(a, b): return a[0] > b[0])
	for o in order:
		var it: Dictionary = o[1]
		var dist: float = o[0]
		var sp := cam.unproject_position(it.pos)
		var vs := get_viewport_rect().size
		if sp.x < -200 or sp.y < -100 or sp.x > vs.x + 200 or sp.y > vs.y + 100:
			continue
		var far := clampf((dist - 20.0) / 100.0, 0.0, 1.0)
		var fs := int(lerpf(17.0, 12.0, far) * (1.12 if it.big else 1.0))
		if far > 0.55 and not it.big:
			fs = int(fs * 0.9)
		_draw_label(sp, it.name, it.pop, it.civ, fs, it.big)


func _draw_label(sp: Vector2, text: String, pop: int, civ: int, fs: int, big: bool) -> void:
	var ink := Palette.INK
	var parchment := Color("#efe2bf")
	var tw := font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x
	var pop_s := str(pop)
	var pw := font.get_string_size(pop_s, HORIZONTAL_ALIGNMENT_LEFT, -1, int(fs * 0.75)).x
	var h := fs * 1.45
	var seal := h * 0.62
	var w := tw + pw + seal * 2.0 + fs * 1.2
	var x0 := sp.x - w * 0.5
	var y0 := sp.y - h * 0.5
	var notch := h * 0.32
	# ribbon tails behind
	var tail := PackedVector2Array([Vector2(x0 - h * 0.45, y0 + h * 0.2), Vector2(x0 + 4, y0 + h * 0.2), Vector2(x0 + 4, y0 + h * 1.12),
		Vector2(x0 - h * 0.45, y0 + h * 1.12), Vector2(x0 - h * 0.45 + notch, y0 + h * 0.66)])
	var tail2 := PackedVector2Array()
	for v in tail:
		tail2.append(Vector2(2.0 * sp.x - v.x, v.y))
	tail2.reverse()
	for tpoly in [tail, tail2]:
		if Geometry2D.triangulate_polygon(tpoly).size() > 0:
			draw_colored_polygon(tpoly, parchment.darkened(0.16))
		var closed: PackedVector2Array = tpoly.duplicate()
		closed.append(tpoly[0])
		draw_polyline(closed, ink, 1.2, true)
	# main band with a soft shadow
	var band := PackedVector2Array([Vector2(x0, y0), Vector2(x0 + w, y0), Vector2(x0 + w, y0 + h), Vector2(x0, y0 + h)])
	var shadow := PackedVector2Array()
	for v in band:
		shadow.append(v + Vector2(2, 3))
	draw_colored_polygon(shadow, Color(0.1, 0.07, 0.05, 0.25))
	draw_colored_polygon(band, parchment)
	var closed_b := band.duplicate()
	closed_b.append(band[0])
	draw_polyline(closed_b, ink, 1.4, true)
	draw_line(Vector2(x0 + 3, y0 + 3), Vector2(x0 + w - 3, y0 + 3), Color(ink, 0.35), 1.0, true)
	draw_line(Vector2(x0 + 3, y0 + h - 3), Vector2(x0 + w - 3, y0 + h - 3), Color(ink, 0.35), 1.0, true)
	# civ seal with emblem
	var sc := Vector2(x0 + seal * 0.95, y0 + h * 0.5)
	draw_circle(sc, seal * 0.78, Palette.CIV[civ % 8])
	draw_arc(sc, seal * 0.78, 0, TAU, 28, ink, 1.4, true)
	_emblem(sc, seal * 0.5, civ, ink if civ % 8 == 3 else Color("#f6eedb"), ink)
	# name and population
	var ty := y0 + h * 0.5 + fs * 0.34
	draw_string(font, Vector2(x0 + seal * 1.9, ty), text, HORIZONTAL_ALIGNMENT_LEFT, -1, fs, ink)
	draw_string(font, Vector2(x0 + seal * 1.9 + tw + fs * 0.45, ty), pop_s, HORIZONTAL_ALIGNMENT_LEFT, -1, int(fs * 0.75), Color(ink, 0.7))
	if big:
		# capital: a small crown of three points above the band
		var cx := sp.x
		var crown := PackedVector2Array([Vector2(cx - 9, y0 - 1), Vector2(cx - 9, y0 - 9), Vector2(cx - 4.5, y0 - 4), Vector2(cx, y0 - 11),
			Vector2(cx + 4.5, y0 - 4), Vector2(cx + 9, y0 - 9), Vector2(cx + 9, y0 - 1)])
		draw_colored_polygon(crown, Palette.CIV[civ % 8])
		var cc := crown.duplicate()
		cc.append(crown[0])
		draw_polyline(cc, ink, 1.2, true)


func _emblem(c: Vector2, r: float, civ: int, fill: Color, ink: Color) -> void:
	match civ % 8:
		0: # sun
			for i in 8:
				var a := TAU * i / 8.0
				draw_line(c + Vector2(cos(a), sin(a)) * r * 0.55, c + Vector2(cos(a), sin(a)) * r * 1.0, fill, 1.6, true)
			draw_circle(c, r * 0.42, fill)
		1: # wave
			for row in 2:
				var pts := PackedVector2Array()
				for i in 9:
					var x := -r + 2.0 * r * i / 8.0
					pts.append(c + Vector2(x, (row - 0.5) * r * 0.75 + sin(i * 1.4) * r * 0.22))
				draw_polyline(pts, fill, 1.8, true)
		2: # leaf
			var pts := PackedVector2Array()
			for i in 16:
				var t := TAU * i / 16.0
				var v := Vector2(cos(t) * r * 0.95, sin(t) * r * 0.45 * (1.0 - 0.3 * cos(t)))
				pts.append(c + v.rotated(-0.6))
			if Geometry2D.triangulate_polygon(pts).size() > 0:
				draw_colored_polygon(pts, fill)
			draw_line(c + Vector2(-r * 0.8, 0).rotated(-0.6), c + Vector2(r * 0.8, 0).rotated(-0.6), Palette.CIV[2], 1.2, true)
		3: # star
			var pts := PackedVector2Array()
			for i in 10:
				var a := -PI * 0.5 + TAU * i / 10.0
				var rr := r * (1.0 if i % 2 == 0 else 0.42)
				pts.append(c + Vector2(cos(a), sin(a)) * rr)
			draw_colored_polygon(pts, fill)
			var cl := pts.duplicate()
			cl.append(pts[0])
			draw_polyline(cl, ink, 0.8, true)
		_:
			draw_circle(c, r * 0.5, fill)
