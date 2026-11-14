## Faith & Fief (E5) civic buildings: the university (LRN-08), the hospital (MED-07), the clock tower (MEC-08) and the print shop (COM-10),
## docs/MVP/32 §4.6 to §4.9.
##
## Not wired to the simulation yet (docs/MVP/32 §9); photographed in the Fief gallery (LowPoly3DView.show_fief_gallery). Same units as the rest of the kit
## (1.0 = one tile column, an adult is ~0.1 tall). Buildings face +Z; TAG_CIV faces take the owner's colour. Placement is docs/MVP/31a's: the university
## is the Learning hex's whole-hex upgrade of the academy (same hex, same facing, larger), the hospital a half in the Health hex, the clock tower a small
## landmark of the Market, the print shop a small Workshops building. Each shows its working state on the building (docs/MVP/32 §8.1):
##
##   university        a collegiate range with an arcade, pointed upper windows, a bell turret and two wings round a court behind a gate tower with the book
##                     sign, lit windows and open doors: a staffed university
##   university_idle   the same with shutters over the windows and the arcade closed: unstaffed
##   hospital          a long low hall with a covered walk, a chapel gable carrying the cross-and-bowl sign, an open porch with white beds and a herb garden
##   hospital_idle     shuttered, the garden dead: empty
##   clock_tower       a slim square tower with a clock face on each side, an open belfry under a pyramid cap and a weathervane
##   print_shop        a narrow timber-framed house with the press in its open ground floor, paper hung to dry in the loft and a type-block sign
##   print_shop_idle   shuttered, no press work and no paper hung
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const LIMESTONE := Color("#cbc3ad")
const LIMESTONE_LIGHT := Color("#ded7c2")
const LIMESTONE_DARK := Color("#a69f8a")
const PLASTER := Color("#e7dcc2")
const SLATE := Color("#58616b")
const SLATE_DARK := Color("#444b54")
const ROOF := Color("#a8492c")
const ROOF_DARK := Color("#7f3622")
const ARCH := Color("#2a2119")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const GOLD := Color("#c9973c")
const LIT := Color("#ffe7a0")
const GLASS_DARK := Color("#39485a")
const GRASS := Color("#8ea548")
const HERB := Color("#6f9a3c")
const HERB_DEAD := Color("#8a7a4c")
const CLOTH_WHITE := Color("#f0ece0")
const RED_CROSS := Color("#b8352c")
const PAPER := Color("#f4efe2")
const INK := Color("#2b2620")
const IRON := Color("#4a4f57")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE
const GRASS_TAG := M7MeshBuilder.TAG_GRASS

static func build(kit) -> void:
	kit.meshes["university"] = _university(kit, true)
	kit.meshes["university_idle"] = _university(kit, false)
	kit.meshes["hospital"] = _hospital(kit, true)
	kit.meshes["hospital_idle"] = _hospital(kit, false)
	kit.meshes["clock_tower"] = _clock_tower(kit)
	kit.meshes["print_shop"] = _print_shop(kit, true)
	kit.meshes["print_shop_idle"] = _print_shop(kit, false)

# ------------------------------------------------------------------ shared pieces

static func _roof_z(b: M7MeshBuilder, cx: float, hx: float, z0: float, z1: float, y: float, rise: float, col: Color, end_col = null) -> void:
	var inner := Vector3(cx, y - 0.01, (z0 + z1) * 0.5)
	for side in [-1.0, 1.0]:
		var c: Color = col if side > 0.0 else col.darkened(0.14)
		b.quad(Vector3(cx + side * hx, y, z0), Vector3(cx + side * hx, y, z1), Vector3(cx, y + rise, z1), Vector3(cx, y + rise, z0), c, inner)
	var ec: Color = (end_col if end_col != null else col.darkened(0.22))
	for z in [z0, z1]:
		b.tri(Vector3(cx - hx, y, z), Vector3(cx + hx, y, z), Vector3(cx, y + rise, z), ec, inner)

static func _roof_x(b: M7MeshBuilder, cz: float, hz: float, x0: float, x1: float, y: float, rise: float, col: Color, end_col = null) -> void:
	var inner := Vector3((x0 + x1) * 0.5, y - 0.01, cz)
	for side in [-1.0, 1.0]:
		var c: Color = col if side > 0.0 else col.darkened(0.14)
		b.quad(Vector3(x0, y, cz + side * hz), Vector3(x1, y, cz + side * hz), Vector3(x1, y + rise, cz), Vector3(x0, y + rise, cz), c, inner)
	var ec: Color = (end_col if end_col != null else col.darkened(0.22))
	for x in [x0, x1]:
		b.tri(Vector3(x, y, cz - hz), Vector3(x, y, cz + hz), Vector3(x, y + rise, cz), ec, inner)

## A pointed window on a wall facing `dir`: a dark surround under a pointed head and a pane. `lit` makes the pane a warm glow, otherwise it is dark
## glass, or a timber shutter when `shut`.
static func _win(b: M7MeshBuilder, p: Vector3, w: float, h: float, dir: Vector3, lit: bool, shut: bool) -> void:
	var across := Vector3(-dir.z, 0.0, dir.x)
	var d := dir * 0.0025
	var away := p - dir
	b.quad(p + across * (-w * 0.55) + d, p + across * (w * 0.55) + d, p + across * (w * 0.55) + Vector3(0, h, 0) + d, p + across * (-w * 0.55) + Vector3(0, h, 0) + d,
		LIMESTONE_DARK, away)
	b.tri(p + across * (-w * 0.55) + Vector3(0, h, 0) + d, p + across * (w * 0.55) + Vector3(0, h, 0) + d, p + Vector3(0, h + w * 0.7, 0) + d, LIMESTONE_DARK, away)
	var tag := b.tag
	var col := LIT if lit else (WOOD if shut else GLASS_DARK)
	if lit:
		b.tag = EMISSIVE
	var d2 := dir * 0.004
	b.quad(p + across * (-w * 0.38) + d2, p + across * (w * 0.38) + d2, p + across * (w * 0.38) + Vector3(0, h * 0.94, 0) + d2,
		p + across * (-w * 0.38) + Vector3(0, h * 0.94, 0) + d2, col, away)
	b.tri(p + across * (-w * 0.38) + Vector3(0, h * 0.94, 0) + d2, p + across * (w * 0.38) + Vector3(0, h * 0.94, 0) + d2,
		p + Vector3(0, h * 0.94 + w * 0.55, 0) + d2, col, away)
	b.tag = tag
	if shut and not lit:
		b.box(p + Vector3(0, h * 0.5, 0) + dir * 0.0045, Vector3(0.0035 if absf(dir.x) > 0.5 else w * 0.35, h * 0.9, w * 0.35 if absf(dir.x) > 0.5 else 0.0035), WOOD_LIGHT)

## A flat disc facing `dir` (clock faces, signs): `n` triangles about the centre.
static func _disc(b: M7MeshBuilder, p: Vector3, r: float, dir: Vector3, col: Color, n: int = 12) -> void:
	var along := Vector3(-dir.z, 0.0, dir.x)
	for i in n:
		var a0 := TAU * i / n
		var a1 := TAU * (i + 1) / n
		b.tri(p, p + along * cos(a0) * r + Vector3(0, sin(a0) * r, 0), p + along * cos(a1) * r + Vector3(0, sin(a1) * r, 0), col, p - dir)

## The open-book sign: a dark cover, two pale pages and a gold edge, on a plate facing +Z, centred on p.
static func _book(b: M7MeshBuilder, p: Vector3) -> void:
	b.box(p + Vector3(0, 0, 0.0), Vector3(0.05, 0.036, 0.004), LIMESTONE_DARK)
	b.box(p + Vector3(0, 0, 0.003), Vector3(0.044, 0.03, 0.003), WOOD)
	for sx in [-1.0, 1.0]:
		b.quad(p + Vector3(sx * 0.0015, -0.012, 0.005), p + Vector3(sx * 0.021, -0.011, 0.0048), p + Vector3(sx * 0.021, 0.013, 0.0048), p + Vector3(sx * 0.0015, 0.012, 0.005),
			PAPER, p + Vector3(0, 0, -1.0))
	b.box(p + Vector3(0, 0, 0.0055), Vector3(0.002, 0.026, 0.002), GOLD)

# ------------------------------------------------------------------ university

static func _university(kit, staffed: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1500 + (0 if staffed else 1))
	b.tag = PLAIN
	var hx := 0.25
	# a low stone plinth under the whole college, the grass of the court, a paved walk to the gate
	b.box(Vector3(0, 0.008, 0.0), Vector3(hx * 2.0 + 0.02, 0.016, 0.46), STONE_DARK, STONE)
	b.tag = GRASS_TAG
	b.box(Vector3(0, 0.0175, 0.1), Vector3(0.31, 0.003, 0.2), GRASS)
	b.tag = PLAIN
	b.box(Vector3(0, 0.02, 0.105), Vector3(0.03, 0.003, 0.2), STONE_LIGHT)
	# the main range at the back: two storeys of dressed stone, arcade below and pointed windows above, a slate roof
	var rh := 0.092
	b.box(Vector3(0, 0.016 + rh * 0.5, -0.1), Vector3(hx * 2.0 - 0.01, rh, 0.2), LIMESTONE, LIMESTONE_LIGHT)
	_roof_x(b, -0.1, 0.118, -hx - 0.006, hx + 0.006, 0.016 + rh, 0.08, SLATE, LIMESTONE)
	b.box(Vector3(0, 0.016 + rh * 0.5 - 0.012, 0.0), Vector3(hx * 2.0 - 0.01, 0.004, 0.004), LIMESTONE_LIGHT)
	for k in 7:
		var x := -0.18 + 0.06 * k
		var open := staffed
		b.box(Vector3(x, 0.04, 0.001), Vector3(0.032, 0.046, 0.006), ARCH)
		b.tri(Vector3(x - 0.016, 0.063, 0.004), Vector3(x + 0.016, 0.063, 0.004), Vector3(x, 0.082, 0.004), ARCH, Vector3(x, 0.05, -0.2))
		if not open:
			b.box(Vector3(x, 0.044, 0.005), Vector3(0.03, 0.054, 0.003), WOOD)
		if k < 6:
			b.box(Vector3(x + 0.03, 0.04, 0.003), Vector3(0.012, 0.05, 0.008), LIMESTONE_LIGHT)
	for k in 7:
		_win(b, Vector3(-0.18 + 0.06 * k, 0.086, 0.002), 0.02, 0.036, Vector3(0, 0, 1), staffed, not staffed)
	# the owner's frieze under the eaves and a pair of buttresses at the corners
	b.tag = CIV
	b.box(Vector3(0, 0.1, 0.0035), Vector3(hx * 2.0 - 0.03, 0.008, 0.004), Color.WHITE)
	b.tag = PLAIN
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * (hx - 0.008), 0.05, 0.004), Vector3(0.014, 0.1, 0.012), LIMESTONE_LIGHT, LIMESTONE)
	# the bell turret on the ridge: an octagonal lantern with louvres and a slate spire, a gold finial
	var tp := Vector3(0, 0.016 + rh + 0.08, -0.1)
	b.prism(tp + Vector3(0, -0.004, 0), 0.026, 0.024, 0.044, 8, LIMESTONE, LIMESTONE_LIGHT, PI / 8.0)
	for k in 8:
		var a := TAU * k / 8.0 + PI / 8.0
		b.box(tp + Vector3(cos(a) * 0.023, 0.018, sin(a) * 0.023), Vector3(0.007, 0.022, 0.007), ARCH)
	b.prism(tp + Vector3(0, 0.04, 0), 0.03, 0.0, 0.085, 8, SLATE, SLATE_DARK, PI / 8.0)
	b.pole(tp + Vector3(0, 0.124, 0), tp + Vector3(0, 0.152, 0), 0.0024, GOLD)
	# the two wings, forward from the range at each end: a storey and a half, slate roofs with a gable toward the gate, windows facing the court
	for sx in [-1.0, 1.0]:
		var cx: float = sx * 0.205
		b.box(Vector3(cx, 0.016 + 0.034, 0.1), Vector3(0.08, 0.068, 0.2), LIMESTONE, LIMESTONE_LIGHT)
		_roof_z(b, cx, 0.054, -0.012, 0.212, 0.016 + 0.068, 0.054, SLATE, LIMESTONE)
		for k in 3:
			_win(b, Vector3(cx - sx * 0.0405, 0.034, 0.04 + 0.06 * k), 0.016, 0.03, Vector3(-sx, 0, 0), staffed, not staffed)
		# the owner's banner on the wing's front gable
		b.tag = CIV
		var bp := Vector3(cx, 0.016 + 0.068 + 0.05, 0.212)
		b.pole(bp, bp + Vector3(0, 0.05, 0), 0.0022, WOOD)
		b.quad(bp + Vector3(0, 0.05, 0), bp + Vector3(0.05, 0.042, 0), bp + Vector3(0.05, 0.022, 0), bp + Vector3(0, 0.028, 0), Color.WHITE, bp + Vector3(0, 0.03, 1.0))
		b.tag = PLAIN
	# the gate wall between the wings with a gate tower carrying the book sign
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * 0.105, 0.016 + 0.017, 0.2), Vector3(0.1, 0.034, 0.016), LIMESTONE, LIMESTONE_LIGHT)
	b.box(Vector3(0, 0.016 + 0.045, 0.2), Vector3(0.1, 0.09, 0.036), LIMESTONE, LIMESTONE_LIGHT)
	b.prism(Vector3(0, 0.016 + 0.09, 0.2), 0.082, 0.0, 0.05, 4, SLATE, SLATE_DARK, PI / 4.0)
	b.box(Vector3(0, 0.036, 0.2185), Vector3(0.036, 0.04, 0.004), ARCH)
	b.tri(Vector3(-0.018, 0.056, 0.2205), Vector3(0.018, 0.056, 0.2205), Vector3(0, 0.074, 0.2205), ARCH, Vector3(0, 0.05, 0.1))
	if not staffed:
		b.box(Vector3(0, 0.04, 0.222), Vector3(0.034, 0.048, 0.003), WOOD)
	_book(b, Vector3(0, 0.086, 0.2205))
	# a scholar on the steps when staffed is the rig's; a sundial in the court
	b.prism(Vector3(0, 0.02, 0.1), 0.01, 0.007, 0.014, 6, STONE_LIGHT, STONE)
	return b.commit()

# ------------------------------------------------------------------ hospital

static func _hospital(kit, staffed: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1510 + (0 if staffed else 1))
	b.tag = PLAIN
	b.box(Vector3(0, 0.005, 0.0), Vector3(0.4, 0.01, 0.3), STONE_DARK, STONE)
	# the long hall: plastered walls under a tiled roof, windows along the front
	var hy := 0.056
	b.box(Vector3(-0.045, 0.01 + hy * 0.5, -0.06), Vector3(0.27, hy, 0.12), PLASTER, LIMESTONE_LIGHT)
	_roof_x(b, -0.06, 0.07, -0.19, 0.1, 0.01 + hy, 0.048, ROOF)
	for k in 5:
		_win(b, Vector3(-0.15 + 0.05 * k, 0.034, 0.0015), 0.014, 0.024, Vector3(0, 0, 1), staffed, not staffed)
	# the covered walk along the front: posts and a lean-to of tiles
	for k in 6:
		var x := -0.17 + 0.054 * k
		b.pole(Vector3(x, 0.01, 0.06), Vector3(x, 0.05, 0.06), 0.0032, WOOD)
	b.quad(Vector3(-0.18, 0.056, 0.003), Vector3(0.1, 0.056, 0.003), Vector3(0.1, 0.046, 0.064), Vector3(-0.18, 0.046, 0.064), ROOF.darkened(0.08), Vector3(-0.04, 0.0, 0.03))
	# the chapel gable at the east end, higher than the hall, with the cross-and-bowl sign and a cross on top
	b.box(Vector3(0.14, 0.01 + 0.036, -0.045), Vector3(0.09, 0.072, 0.15), PLASTER, LIMESTONE_LIGHT)
	_roof_z(b, 0.14, 0.056, -0.125, 0.04, 0.01 + 0.072, 0.05, ROOF)
	b.box(Vector3(0.14, 0.05, 0.0305), Vector3(0.022, 0.04, 0.004), ARCH if staffed else WOOD)
	b.box(Vector3(0.14, 0.092, 0.04), Vector3(0.034, 0.034, 0.004), CLOTH_WHITE)
	b.box(Vector3(0.14, 0.092, 0.0425), Vector3(0.006, 0.026, 0.002), RED_CROSS)
	b.box(Vector3(0.14, 0.094, 0.0425), Vector3(0.022, 0.006, 0.002), RED_CROSS)
	b.prism(Vector3(0.162, 0.078, 0.0415), 0.007, 0.005, 0.003, 7, GOLD, GOLD)
	b.pole(Vector3(0.14, 0.158, 0.04), Vector3(0.14, 0.18, 0.04), 0.0022, WOOD)
	b.pole(Vector3(0.132, 0.17, 0.04), Vector3(0.148, 0.17, 0.04), 0.002, WOOD)
	# the open porch at the west end: posts, a roof and two beds with white linen, a nurse's stool
	for sx in [-0.2, -0.13]:
		for sz in [-0.09, -0.01]:
			b.pole(Vector3(sx, 0.01, sz), Vector3(sx, 0.05, sz), 0.0035, WOOD)
	_roof_x(b, -0.05, 0.05, -0.215, -0.115, 0.05, 0.022, ROOF.darkened(0.04))
	for k in 2:
		var bz := -0.065 + 0.04 * k
		b.box(Vector3(-0.16, 0.022, bz), Vector3(0.05, 0.012, 0.022), WOOD_LIGHT)
		b.box(Vector3(-0.16, 0.03, bz), Vector3(0.046, 0.006, 0.02), CLOTH_WHITE if staffed else PLASTER.darkened(0.1))
	# the herb garden in front: fenced beds with the herbs green when tended
	b.box(Vector3(-0.07, 0.011, 0.115), Vector3(0.2, 0.002, 0.06), Color("#6a523a"))
	for k in 4:
		var hx := -0.14 + 0.047 * k
		b.box(Vector3(hx, 0.016, 0.115), Vector3(0.034, 0.008, 0.044), HERB if staffed else HERB_DEAD)
		if staffed:
			b.blob(Vector3(hx, 0.024, 0.115), Vector3(0.014, 0.008, 0.016), 5, 2, HERB.lightened(0.12), 0.2)
	for sx in [-0.175, 0.035]:
		b.pole(Vector3(sx, 0.01, 0.085), Vector3(sx, 0.03, 0.085), 0.0022, WOOD)
		b.pole(Vector3(sx, 0.01, 0.145), Vector3(sx, 0.03, 0.145), 0.0022, WOOD)
	b.pole(Vector3(-0.175, 0.028, 0.145), Vector3(0.035, 0.028, 0.145), 0.0016, WOOD)
	# a chimney for the kitchen at the back
	b.prism(Vector3(-0.1, 0.01 + hy + 0.03, -0.09), 0.011, 0.009, 0.04, 4, STONE, STONE_LIGHT, PI / 4.0)
	return b.commit()

# ------------------------------------------------------------------ clock tower

static func _clock_tower(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1520)
	b.tag = PLAIN
	b.prism(Vector3(0, -0.01, 0), 0.07, 0.06, 0.03, 4, STONE_DARK, STONE, PI / 4.0)
	# the shaft: plastered stone narrowing a little, with a string course, a door and two slit windows
	b.prism(Vector3(0, 0.018, 0), 0.056, 0.047, 0.17, 4, LIMESTONE, LIMESTONE_LIGHT, PI / 4.0)
	b.prism(Vector3(0, 0.1, 0), 0.054, 0.054, 0.006, 4, LIMESTONE_LIGHT, LIMESTONE_LIGHT, PI / 4.0)
	b.box(Vector3(0, 0.03, 0.0385), Vector3(0.022, 0.032, 0.006), ARCH)
	b.box(Vector3(0, 0.07, 0.0368), Vector3(0.006, 0.024, 0.004), ARCH)
	# the clock stage: a thicker drum of stone with a clock face on each side
	b.prism(Vector3(0, 0.188, 0), 0.062, 0.062, 0.002, 4, STONE_LIGHT, STONE_LIGHT, PI / 4.0)
	b.prism(Vector3(0, 0.19, 0), 0.052, 0.05, 0.06, 4, LIMESTONE, LIMESTONE_LIGHT, PI / 4.0)
	for dir in [Vector3(0, 0, 1), Vector3(0, 0, -1), Vector3(1, 0, 0), Vector3(-1, 0, 0)]:
		var o: Vector3 = Vector3(0, 0.22, 0) + dir * 0.0375
		_disc(b, o, 0.021, dir, GOLD.darkened(0.2))
		_disc(b, o + dir * 0.001, 0.018, dir, Color("#f0e9d4"))
		var along := Vector3(-dir.z, 0.0, dir.x)
		# twelve ticks and two hands, the hour hand at four and the minute hand at twelve
		for k in 12:
			var a := TAU * k / 12.0
			b.box(o + dir * 0.0025 + along * sin(a) * 0.0155 + Vector3(0, cos(a) * 0.0155, 0), Vector3(0.002, 0.0035, 0.0015) if absf(dir.z) > 0.5 else Vector3(0.0015, 0.0035, 0.002), INK)
		b.pole(o + dir * 0.003, o + dir * 0.003 + Vector3(0, 0.014, 0), 0.0012, INK)
		b.pole(o + dir * 0.003, o + dir * 0.003 + along * 0.008 - Vector3(0, 0.005, 0), 0.0016, INK)
	# the belfry: open arches on the four sides under a cornice, the bell inside, then a slate pyramid cap and a weathervane
	b.prism(Vector3(0, 0.25, 0), 0.058, 0.058, 0.006, 4, LIMESTONE_LIGHT, LIMESTONE_LIGHT, PI / 4.0)
	b.prism(Vector3(0, 0.256, 0), 0.05, 0.048, 0.04, 4, LIMESTONE, LIMESTONE_LIGHT, PI / 4.0)
	for dir in [Vector3(0, 0, 1), Vector3(0, 0, -1), Vector3(1, 0, 0), Vector3(-1, 0, 0)]:
		var o2: Vector3 = Vector3(0, 0.274, 0) + dir * 0.0345
		var sz := Vector3(0.022, 0.03, 0.004) if absf(dir.z) > 0.5 else Vector3(0.004, 0.03, 0.022)
		b.box(o2, sz, ARCH)
	b.prism(Vector3(0, 0.262, 0), 0.01, 0.006, 0.02, 6, GOLD.darkened(0.15), GOLD)
	b.prism(Vector3(0, 0.296, 0), 0.062, 0.0, 0.08, 4, SLATE, SLATE_DARK, PI / 4.0)
	b.pole(Vector3(0, 0.374, 0), Vector3(0, 0.404, 0), 0.0024, GOLD)
	b.quad(Vector3(0, 0.4, 0), Vector3(0.026, 0.396, 0), Vector3(0.026, 0.388, 0), Vector3(0, 0.39, 0), GOLD, Vector3(0, 0.395, 1.0))
	return b.commit()

# ------------------------------------------------------------------ print shop

static func _print_shop(kit, working: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1530 + (0 if working else 1))
	b.tag = PLAIN
	var hx := 0.052
	var hz := 0.05
	b.box(Vector3(0, 0.006, 0), Vector3(hx * 2.0 + 0.02, 0.012, hz * 2.0 + 0.02), STONE_DARK, STONE)
	# a timber-framed house of two storeys, the upper one jettied out over the street: plaster panels between dark beams
	b.box(Vector3(0, 0.012 + 0.02, 0), Vector3(hx * 2.0, 0.04, hz * 2.0), PLASTER, LIMESTONE_LIGHT)
	b.box(Vector3(0, 0.052 + 0.02, 0.005), Vector3(hx * 2.0 + 0.008, 0.04, hz * 2.0 + 0.01), PLASTER, LIMESTONE_LIGHT)
	for x in [-hx, 0.0, hx]:
		b.box(Vector3(x, 0.052 + 0.02, hz + 0.011), Vector3(0.006, 0.04, 0.003), WOOD)
	b.box(Vector3(0, 0.052, hz + 0.011), Vector3(hx * 2.0 + 0.008, 0.006, 0.004), WOOD)
	b.box(Vector3(0, 0.092, hz + 0.011), Vector3(hx * 2.0 + 0.008, 0.006, 0.004), WOOD)
	_roof_z(b, 0.0, hx + 0.012, -hz - 0.006, hz + 0.02, 0.092, 0.05, ROOF)
	# the ground floor open to the street: the press (two posts, a cross beam, a screw and bar, a bed with a forme) and a table of type
	b.box(Vector3(0, 0.028, hz + 0.001), Vector3(0.07, 0.034, 0.004), ARCH)
	for sx in [-0.026, 0.026]:
		b.pole(Vector3(sx, 0.012, hz - 0.012), Vector3(sx, 0.048, hz - 0.012), 0.0032, WOOD)
	b.box(Vector3(0, 0.046, hz - 0.012), Vector3(0.062, 0.006, 0.01), WOOD)
	b.pole(Vector3(0, 0.014, hz - 0.012), Vector3(0, 0.046, hz - 0.012), 0.0034, IRON)
	b.box(Vector3(0, 0.022, hz - 0.012), Vector3(0.04, 0.005, 0.03), WOOD_LIGHT)
	b.box(Vector3(0, 0.026, hz - 0.012), Vector3(0.03, 0.004, 0.022), INK if working else WOOD)
	if working:
		b.pole(Vector3(0, 0.034, hz - 0.012), Vector3(0.022, 0.034, hz - 0.012), 0.0018, WOOD)
	# the loft: a window with a shutter and a line of printed sheets drying under the eaves (hung only when work goes on)
	_win(b, Vector3(-0.024, 0.06, hz + 0.0115), 0.014, 0.024, Vector3(0, 0, 1), false, not working)
	if working:
		b.pole(Vector3(-0.044, 0.108, hz + 0.014), Vector3(0.044, 0.108, hz + 0.014), 0.0012, WOOD)
		for k in 5:
			var sx2 := -0.036 + 0.018 * k
			b.quad(Vector3(sx2, 0.108, hz + 0.0145), Vector3(sx2 + 0.012, 0.108, hz + 0.0145), Vector3(sx2 + 0.012, 0.09, hz + 0.0145), Vector3(sx2, 0.092, hz + 0.0145), PAPER, Vector3(0, 0.1, -1.0))
			b.quad(Vector3(sx2, 0.108, hz + 0.0145), Vector3(sx2 + 0.012, 0.108, hz + 0.0145), Vector3(sx2 + 0.012, 0.09, hz + 0.0145), Vector3(sx2, 0.092, hz + 0.0145), PAPER.darkened(0.06), Vector3(0, 0.1, 1.0))
			b.box(Vector3(sx2 + 0.006, 0.1, hz + 0.0148), Vector3(0.008, 0.002, 0.0005), INK)
	# the hanging sign on an iron bracket at the corner: a block of type with a letter on it
	b.pole(Vector3(hx + 0.012, 0.07, hz + 0.008), Vector3(hx + 0.05, 0.07, hz + 0.008), 0.0022, IRON)
	b.box(Vector3(hx + 0.04, 0.056, hz + 0.008), Vector3(0.026, 0.026, 0.004), CLOTH_WHITE)
	b.box(Vector3(hx + 0.04, 0.056, hz + 0.0105), Vector3(0.006, 0.016, 0.001), INK)
	b.box(Vector3(hx + 0.04, 0.062, hz + 0.0105), Vector3(0.014, 0.004, 0.001), INK)
	b.pole(Vector3(hx + 0.04, 0.07, hz + 0.008), Vector3(hx + 0.04, 0.0685, hz + 0.008), 0.0009, IRON)
	# a chimney for the ink heater
	b.prism(Vector3(0.03, 0.122, -0.02), 0.009, 0.007, 0.026, 4, STONE, STONE_LIGHT, PI / 4.0)
	return b.commit()
