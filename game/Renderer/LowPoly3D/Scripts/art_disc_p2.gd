## Discovery (E6) polish, the P2 tier of docs/MVP/39 §1: the embassy flag (DIP-09), the observatory (INQ-07), the greenhouse (CER-11), the news board (COM-11), the
## crops the Columbian Exchange brings (FOR-10) and the extra cargo of the age's workshops (CER-11, MEC-09, CHM-08, COM-11, WAY-10, INQ-07).
##
## Not wired to the simulation yet (docs/MVP/39 §9); photographed in the Discovery gallery (LowPoly3DView.show_discovery_gallery). Same units as the rest of the kit
## (1.0 = one tile column, an adult is ~0.1 tall). Buildings face +Z; TAG_CIV faces take the owner's colour (or, for the embassy flag, the *rival's*: it is set on the
## Heart of the settlement that hosts it). Several of these have no typed building behind them yet (doc 39 §4.10): they are drawn only when the sim holds the thing.
##
##   embassy_flag     DIP-09: a tall pole on a small stone plinth with a flag in the guest civilization's colour and a narrow white pennant of parley beneath it, a brass
##                    finial; set on the Heart of the settlement that hosts a resident embassy
##   observatory      INQ-07: a small round stone tower with a green copper dome cut by a slit, a brass telescope poking out of it, a balcony and a stair turret;
##                    a small building of the Learning hex beside the university (docs/MVP/31a)
##   greenhouse       CER-11: a free-standing glasshouse: a low stone base, a timber frame, a gabled roof and glazed ends in plate glass, potted plants showing through
##                    and a door; outside the wall on farmland (docs/MVP/39 §4.10)
##   news_board       COM-11: a covered timber notice board on two posts with printed broadsheets pinned to it, a barrel and a stack of fresh pamphlets at its foot
##   crop_maize       FOR-10: a cob of maize in its husk, bright yellow kernels and green leaves, a floating icon above fields that gain a new staple
##   crop_potato      FOR-10: three potatoes with a green sprout
##   crop_tobacco     FOR-10: a broad tobacco leaf on a stem
##   cargo_glass      plate glass for a cart or quay: panes standing upright in a straw-packed crate
##   cargo_parts      precision parts: a small crate with brass gears and a spindle on top
##   cargo_acid       sulfuric acid: three green glass carboys in wicker baskets
##   cargo_alkali     soda alkali: sacks of white powder with a dark band, stacked
##   cargo_pamphlets  a stack of printed pamphlets and broadsheets, tied with cord
##   cargo_instruments  a lidded velvet box holding a marine chronometer in its gimbals and a brass telescope laid beside it
extends RefCounted

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const ASHLAR := Color("#d8cdb0")
const ASHLAR_LIGHT := Color("#ece3c8")
const COPPER := Color("#6aa08a")
const COPPER_DARK := Color("#4a7e6a")
const BRASS := Color("#c9973c")
const BRASS_LIGHT := Color("#e0b660")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const WOOD_DARK := Color("#4a3726")
const GLASS_CLEAR := Color("#9cc4d8")
const GLASS_DARK := Color("#2d3d4d")
const LEAF := Color("#4f8a38")
const LEAF_DARK := Color("#3a6a2a")
const PAPER := Color("#f1ead8")
const PAPER_DARK := Color("#d8cdb0")
const INK := Color("#2b2620")
const STRAW := Color("#d8c27a")
const TILE := Color("#a8492c")
const TILE_DARK := Color("#7f3622")
const DOOR := Color("#2a2119")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE

static func build(kit) -> void:
	kit.meshes["embassy_flag"] = _embassy(kit)
	kit.meshes["observatory"] = _observatory(kit)
	kit.meshes["greenhouse"] = _greenhouse(kit)
	kit.meshes["news_board"] = _news_board(kit)
	kit.meshes["crop_maize"] = _maize(kit)
	kit.meshes["crop_potato"] = _potato(kit)
	kit.meshes["crop_tobacco"] = _tobacco(kit)
	kit.meshes["cargo_glass"] = _cargo_glass(kit)
	kit.meshes["cargo_parts"] = _cargo_parts(kit)
	kit.meshes["cargo_acid"] = _cargo_acid(kit)
	kit.meshes["cargo_alkali"] = _cargo_alkali(kit)
	kit.meshes["cargo_pamphlets"] = _cargo_pamphlets(kit)
	kit.meshes["cargo_instruments"] = _cargo_instruments(kit)

# ------------------------------------------------------------------ embassy flag

static func _embassy(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2500)
	b.tag = PLAIN
	b.box(Vector3(0, 0.008, 0), Vector3(0.05, 0.016, 0.05), STONE, STONE_LIGHT)
	b.box(Vector3(0, 0.0185, 0.0), Vector3(0.036, 0.005, 0.036), STONE_LIGHT)
	# a small brass plaque on the plinth's front face
	b.box(Vector3(0, 0.008, 0.0255), Vector3(0.022, 0.009, 0.0015), BRASS)
	b.pole(Vector3(0, 0.02, 0), Vector3(0, 0.2, 0), 0.0028, WOOD, 6)
	b.blob(Vector3(0, 0.206, 0), Vector3(0.0055, 0.0055, 0.0055), 5, 3, BRASS_LIGHT, 0.0)
	# the guest civilization's flag, a broad rectangle with a notched fly, in the colour of the *guest* (the instance colour set by the Heart that hosts it)
	b.tag = CIV
	var top := Vector3(0, 0.196, 0)
	var low := Vector3(0, 0.16, 0)
	for hint in [-1.0, 1.0]:
		var h: float = hint
		b.quad(top, top + Vector3(0.062, -0.004, 0), low + Vector3(0.062, -0.002, 0), low, Color.WHITE, Vector3(0, 0, h))
		b.tri(top + Vector3(0.062, -0.004, 0), low + Vector3(0.062, -0.002, 0), Vector3(0.052, 0.178, 0), Color.WHITE, Vector3(0, 0, h))
	# the narrow white pennant of parley under it
	b.tag = PLAIN
	var ptop := Vector3(0, 0.15, 0)
	for hint in [-1.0, 1.0]:
		var h2: float = hint
		b.tri(ptop, ptop + Vector3(0.045, -0.006, 0), ptop + Vector3(0, -0.016, 0), PAPER, Vector3(0, 0, h2))
	return b.commit()

# ------------------------------------------------------------------ observatory

static func _observatory(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2501)
	b.tag = PLAIN
	b.box(Vector3(0, 0.004, 0), Vector3(0.11, 0.008, 0.11), STONE_DARK, STONE)
	# the round tower in pale stone with a string course and a balcony ring half way up
	b.prism(Vector3(0, 0.008, 0), 0.042, 0.037, 0.1, 12, ASHLAR, ASHLAR_LIGHT)
	b.prism(Vector3(0, 0.054, 0), 0.047, 0.047, 0.005, 12, STONE_LIGHT, STONE_LIGHT)
	for k in 12:
		var a := TAU * k / 12.0
		b.pole(Vector3(cos(a) * 0.0455, 0.059, sin(a) * 0.0455), Vector3(cos(a) * 0.0455, 0.07, sin(a) * 0.0455), 0.0014, STONE_DARK, 3)
	b.prism(Vector3(0, 0.07, 0), 0.0475, 0.0475, 0.0018, 12, STONE_LIGHT, STONE_LIGHT)
	# the door and two narrow windows
	b.box(Vector3(0, 0.022, 0.0405), Vector3(0.016, 0.028, 0.003), DOOR)
	for sx in [-0.017, 0.017]:
		var x: float = sx
		b.box(Vector3(x, 0.082, 0.0395), Vector3(0.007, 0.016, 0.003), GLASS_DARK)
	# a stair turret on the back, a slim round tube with a cap
	b.prism(Vector3(-0.036, 0.008, -0.03), 0.013, 0.012, 0.115, 8, ASHLAR, ASHLAR_LIGHT)
	b.prism(Vector3(-0.036, 0.123, -0.03), 0.0145, 0.0, 0.016, 8, COPPER, COPPER_DARK)
	# the copper dome on a drum, cut by a vertical slit toward +Z, a brass telescope thrust out of it, tilted up
	b.prism(Vector3(0, 0.108, 0), 0.04, 0.04, 0.012, 12, ASHLAR, ASHLAR_LIGHT)
	b.blob(Vector3(0, 0.12, 0), Vector3(0.042, 0.038, 0.042), 12, 4, COPPER, 0.0, COPPER_DARK)
	b.box(Vector3(0, 0.134, 0.025), Vector3(0.011, 0.034, 0.025), DOOR)
	b.pole(Vector3(0, 0.126, 0.012), Vector3(0, 0.168, 0.062), 0.0055, BRASS, 6)
	b.pole(Vector3(0, 0.126, 0.012), Vector3(0, 0.1445, 0.034), 0.0068, BRASS_LIGHT, 6)
	b.blob(Vector3(0, 0.168, 0.062), Vector3(0.0062, 0.0062, 0.0062), 5, 3, GLASS_CLEAR, 0.0)
	b.blob(Vector3(0, 0.16, 0.0), Vector3(0.005, 0.005, 0.005), 4, 2, BRASS_LIGHT, 0.0)
	return b.commit()

# ------------------------------------------------------------------ greenhouse

static func _greenhouse(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2502)
	b.tag = PLAIN
	var w := 0.2
	var d := 0.12
	b.box(Vector3(0, 0.008, 0), Vector3(w + 0.012, 0.016, d + 0.012), STONE_DARK, STONE)
	# glazed walls on four sides in a timber frame, the end walls gabled
	var wh := 0.034
	for sz in [-1.0, 1.0]:
		var z: float = sz
		b.box(Vector3(0, 0.016 + wh * 0.5, z * d * 0.5), Vector3(w, wh, 0.003), GLASS_CLEAR)
	for sx in [-1.0, 1.0]:
		var x: float = sx
		b.box(Vector3(x * w * 0.5, 0.016 + wh * 0.5, 0), Vector3(0.003, wh, d), GLASS_CLEAR)
		b.tri(Vector3(x * w * 0.5, 0.016 + wh, -d * 0.5), Vector3(x * w * 0.5, 0.016 + wh, d * 0.5), Vector3(x * w * 0.5, 0.016 + wh + 0.03, 0), GLASS_CLEAR, Vector3(x * 2.0, 0.03, 0))
	# the roof: two sloped planes of glass over the ridge, with mullions
	var ridge := 0.016 + wh + 0.03
	for sz in [-1.0, 1.0]:
		var z2: float = sz
		b.quad(Vector3(-w * 0.5, 0.016 + wh, z2 * d * 0.5), Vector3(w * 0.5, 0.016 + wh, z2 * d * 0.5), Vector3(w * 0.5, ridge, 0.0), Vector3(-w * 0.5, ridge, 0.0), GLASS_CLEAR.lightened(0.1), Vector3(0, -1.0, 0))
	for k in 9:
		var mx := lerpf(-w * 0.5, w * 0.5, float(k) / 8.0)
		for sz in [-1.0, 1.0]:
			var z3: float = sz
			b.pole(Vector3(mx, 0.016, z3 * d * 0.5), Vector3(mx, 0.016 + wh, z3 * d * 0.5), 0.0014, WOOD, 3)
			b.pole(Vector3(mx, 0.016 + wh, z3 * d * 0.5), Vector3(mx, ridge, 0.0), 0.0014, WOOD, 3)
	b.pole(Vector3(-w * 0.5, ridge, 0.0), Vector3(w * 0.5, ridge, 0.0), 0.0024, WOOD, 4)
	b.pole(Vector3(-w * 0.5, 0.016 + wh, d * 0.5), Vector3(w * 0.5, 0.016 + wh, d * 0.5), 0.002, WOOD, 3)
	b.pole(Vector3(-w * 0.5, 0.016 + wh, -d * 0.5), Vector3(w * 0.5, 0.016 + wh, -d * 0.5), 0.002, WOOD, 3)
	# the door in the front glazing
	b.box(Vector3(0.0, 0.016 + 0.015, d * 0.5 + 0.0018), Vector3(0.024, 0.03, 0.002), WOOD_DARK)
	# potted plants and a tall one showing through: pots on a bench along the back, a palm-like plant in the middle
	b.box(Vector3(0, 0.026, -d * 0.5 + 0.02), Vector3(w - 0.03, 0.006, 0.02), WOOD_LIGHT)
	for k in 6:
		var px := lerpf(-w * 0.5 + 0.02, w * 0.5 - 0.02, float(k) / 5.0)
		b.prism(Vector3(px, 0.029, -d * 0.5 + 0.02), 0.0058, 0.0042, 0.008, 6, Color("#b4673a"), Color("#8a4a28"))
		b.blob(Vector3(px, 0.043, -d * 0.5 + 0.02), Vector3(0.0085, 0.0075, 0.0085), 5, 2, LEAF if k % 2 == 0 else LEAF_DARK, 0.1)
	b.prism(Vector3(0, 0.016, 0.0), 0.007, 0.0055, 0.01, 6, Color("#b4673a"), Color("#8a4a28"))
	b.pole(Vector3(0, 0.026, 0.0), Vector3(0, 0.062, 0.0), 0.0021, WOOD, 4)
	for k in 6:
		var a := TAU * k / 6.0
		b.pole(Vector3(0, 0.062, 0.0), Vector3(cos(a) * 0.016, 0.052, sin(a) * 0.016), 0.0018, LEAF, 3)
	return b.commit()

# ------------------------------------------------------------------ the news board

static func _news_board(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2503)
	b.tag = PLAIN
	b.pole(Vector3(-0.042, 0.0, 0.0), Vector3(-0.042, 0.1, 0.0), 0.0042, WOOD, 4)
	b.pole(Vector3(0.042, 0.0, 0.0), Vector3(0.042, 0.1, 0.0), 0.0042, WOOD, 4)
	# the board, a little pitched roof over it, and the broadsheets pinned in rows: pale sheets with dark ruled lines of type and a bold headline bar
	b.box(Vector3(0, 0.07, 0.0), Vector3(0.096, 0.06, 0.004), WOOD_DARK, WOOD)
	b.box(Vector3(0, 0.104, 0.0), Vector3(0.108, 0.003, 0.02), WOOD_LIGHT)
	b.quad(Vector3(-0.056, 0.1, 0.012), Vector3(0.056, 0.1, 0.012), Vector3(0.056, 0.116, 0.0), Vector3(-0.056, 0.116, 0.0), TILE, Vector3(0, 0.05, -1.0))
	b.quad(Vector3(-0.056, 0.1, -0.012), Vector3(0.056, 0.1, -0.012), Vector3(0.056, 0.116, 0.0), Vector3(-0.056, 0.116, 0.0), TILE_DARK, Vector3(0, 0.05, 1.0))
	var sheets := [[-0.028, 0.082, 0.032, 0.024], [0.012, 0.084, 0.034, 0.022], [0.039, 0.082, 0.022, 0.026], [-0.03, 0.054, 0.028, 0.022], [0.0, 0.056, 0.036, 0.024], [0.034, 0.055, 0.026, 0.02]]
	for sh in sheets:
		var cx: float = sh[0]
		var cy: float = sh[1]
		var sw: float = sh[2]
		var sh_h: float = sh[3]
		b.box(Vector3(cx, cy, 0.0028), Vector3(sw, sh_h, 0.0012), PAPER, PAPER_DARK)
		b.box(Vector3(cx, cy + sh_h * 0.32, 0.0036), Vector3(sw * 0.7, sh_h * 0.14, 0.0006), INK)
		for ln in 3:
			b.box(Vector3(cx, cy - sh_h * 0.05 - ln * sh_h * 0.17, 0.0036), Vector3(sw * 0.8, sh_h * 0.06, 0.0006), INK.lightened(0.35))
	# a barrel and a tied stack of pamphlets at the foot
	b.prism(Vector3(-0.03, 0.0, 0.026), 0.0095, 0.0095, 0.018, 8, WOOD_LIGHT, WOOD_LIGHT.lightened(0.1))
	b.box(Vector3(0.026, 0.006, 0.022), Vector3(0.026, 0.012, 0.018), PAPER, PAPER_DARK)
	b.pole(Vector3(0.026, 0.0075, 0.012), Vector3(0.026, 0.0075, 0.032), 0.0012, Color("#c8b88a"), 3)
	return b.commit()

# ------------------------------------------------------------------ crops of the Columbian Exchange

static func _maize(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2510)
	b.tag = PLAIN
	b.blob(Vector3(0, 0.03, 0), Vector3(0.0095, 0.03, 0.0095), 7, 4, Color("#e8c33a"), 0.05, Color("#d4a92a"))
	for k in 4:
		var a := TAU * k / 4.0 + 0.4
		b.tri(Vector3(0, 0.0, 0), Vector3(cos(a) * 0.014, 0.034, sin(a) * 0.014), Vector3(0, 0.05, 0), LEAF if k % 2 == 0 else LEAF_DARK, Vector3(0, 0.03, 0))
	b.pole(Vector3(0, 0.0, 0), Vector3(0, -0.008, 0), 0.0034, LEAF_DARK, 4)
	return b.commit()

static func _potato(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2511)
	b.tag = PLAIN
	for p in [[-0.012, 0.007, 0.0, 0.0105], [0.011, 0.0065, 0.004, 0.0095], [0.0, 0.0085, -0.011, 0.0085]]:
		b.blob(Vector3(p[0], p[1], p[2]), Vector3(float(p[3]) * 1.25, float(p[3]) * 0.85, float(p[3])), 6, 3, Color("#b08a58"), 0.12, Color("#8a6a3a"))
	b.pole(Vector3(0.0, 0.014, -0.011), Vector3(0.002, 0.03, -0.011), 0.0016, LEAF, 3)
	b.blob(Vector3(0.002, 0.032, -0.011), Vector3(0.007, 0.0045, 0.007), 5, 2, LEAF, 0.1)
	return b.commit()

static func _tobacco(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2512)
	b.tag = PLAIN
	b.pole(Vector3(0, 0.0, 0), Vector3(0, 0.044, 0), 0.0019, LEAF_DARK, 4)
	for k in 5:
		var y := 0.01 + 0.008 * k
		var a := 0.9 * k
		var d := Vector3(cos(a), 0, sin(a))
		var sd := Vector3(-d.z, 0, d.x)
		var tip := Vector3(0, y + 0.006, 0) + d * (0.03 - 0.003 * k)
		b.quad(Vector3(0, y, 0), Vector3(0, y, 0) + d * 0.012 + sd * 0.009, tip, Vector3(0, y, 0) + d * 0.012 - sd * 0.009, LEAF if k % 2 == 0 else LEAF_DARK, Vector3(0, -1.0, 0))
	return b.commit()

# ------------------------------------------------------------------ extra cargo

static func _cargo_base(b: M7MeshBuilder, w: float, d: float) -> void:
	b.box(Vector3(0, 0.0075, 0), Vector3(w, 0.015, d), WOOD_LIGHT, WOOD_LIGHT.lightened(0.06))
	b.box(Vector3(0, 0.0072, d * 0.5 + 0.0008), Vector3(w + 0.002, 0.009, 0.0016), WOOD_DARK)

static func _cargo_glass(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2520)
	b.tag = PLAIN
	_cargo_base(b, 0.07, 0.044)
	b.box(Vector3(0, 0.0185, 0.0), Vector3(0.066, 0.006, 0.04), STRAW, STRAW.lightened(0.1))
	for k in 6:
		b.box(Vector3(-0.025 + 0.01 * k, 0.034, 0.0), Vector3(0.0022, 0.03, 0.036), GLASS_CLEAR.lightened(0.15))
		b.box(Vector3(-0.025 + 0.01 * k, 0.034, 0.0), Vector3(0.0014, 0.0306, 0.0366), GLASS_CLEAR)
	return b.commit()

static func _cargo_parts(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2521)
	b.tag = PLAIN
	_cargo_base(b, 0.06, 0.042)
	b.box(Vector3(0, 0.0185, 0), Vector3(0.056, 0.006, 0.038), Color("#3a342e"))
	# gears laid in the crate: toothed discs of brass of three sizes, and a spindle
	for gp in [[-0.016, 0.0, 0.014], [0.016, 0.004, 0.011], [0.0, -0.01, 0.0085]]:
		var gx: float = gp[0]
		var gz: float = gp[1]
		var gr: float = gp[2]
		b.prism(Vector3(gx, 0.0215, gz), gr, gr, 0.004, 10, BRASS, BRASS_LIGHT)
		for k in 8:
			var a := TAU * k / 8.0
			b.box(Vector3(gx + cos(a) * (gr + 0.0015), 0.0235, gz + sin(a) * (gr + 0.0015)), Vector3(0.0034, 0.004, 0.0034), BRASS)
		b.prism(Vector3(gx, 0.0255, gz), 0.0028, 0.0028, 0.003, 6, STONE_DARK, STONE_DARK)
	b.pole(Vector3(-0.026, 0.0275, -0.016), Vector3(0.026, 0.0275, -0.016), 0.0018, Color("#8d949c"), 4)
	return b.commit()

static func _cargo_acid(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2522)
	b.tag = PLAIN
	b.box(Vector3(0, 0.004, 0), Vector3(0.07, 0.008, 0.044), WOOD_LIGHT, WOOD_LIGHT.lightened(0.05))
	for k in 3:
		var x := -0.022 + 0.022 * k
		# a wicker basket cradling a big green glass carboy with a stopper
		b.prism(Vector3(x, 0.008, 0.0), 0.0105, 0.0125, 0.015, 8, Color("#b08a4e"), Color("#8a6a38"))
		b.blob(Vector3(x, 0.025, 0.0), Vector3(0.0125, 0.0125, 0.0125), 8, 4, Color("#4f9a5a"), 0.0, Color("#3a7a44"))
		b.prism(Vector3(x, 0.034, 0.0), 0.0042, 0.0034, 0.008, 6, Color("#4f9a5a"), Color("#4f9a5a"))
		b.prism(Vector3(x, 0.0415, 0.0), 0.0048, 0.0048, 0.0025, 6, Color("#2a2119"), Color("#2a2119"))
	return b.commit()

static func _cargo_alkali(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2523)
	b.tag = PLAIN
	b.box(Vector3(0, 0.004, 0), Vector3(0.07, 0.008, 0.046), WOOD_LIGHT, WOOD_LIGHT.lightened(0.05))
	for row in 2:
		for k in 3:
			var x := -0.02 + 0.02 * k + (0.008 if row == 1 else 0.0)
			var y := 0.0135 + 0.014 * row
			b.blob(Vector3(x, y, 0.0), Vector3(0.0105, 0.0075, 0.0165), 6, 3, Color("#ebe6d8"), 0.06, Color("#c9c3b0"))
			b.box(Vector3(x, y + 0.0035, 0.0), Vector3(0.0045, 0.002, 0.0235), Color("#3f4a58"))
	return b.commit()

static func _cargo_pamphlets(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2524)
	b.tag = PLAIN
	b.box(Vector3(0, 0.004, 0), Vector3(0.062, 0.008, 0.044), WOOD_LIGHT, WOOD_LIGHT.lightened(0.05))
	for k in 3:
		var x := -0.016 + 0.016 * k
		b.box(Vector3(x, 0.0145, 0.0), Vector3(0.0125, 0.013, 0.03), PAPER, PAPER_DARK)
		b.box(Vector3(x, 0.0145, 0.0), Vector3(0.0128, 0.0012, 0.0302), INK.lightened(0.3))
		b.pole(Vector3(x, 0.0145, -0.015), Vector3(x, 0.0145, 0.015), 0.0011, Color("#c8b88a"), 3)
	b.box(Vector3(-0.004, 0.0275, 0.0), Vector3(0.02, 0.0125, 0.026), PAPER, PAPER_DARK)
	b.box(Vector3(-0.004, 0.0275, 0.0), Vector3(0.0204, 0.0012, 0.0262), INK.lightened(0.3))
	b.box(Vector3(0.016, 0.0225, 0.004), Vector3(0.02, 0.0012, 0.026), PAPER, PAPER_DARK)
	return b.commit()

static func _cargo_instruments(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2525)
	b.tag = PLAIN
	b.box(Vector3(0, 0.007, 0), Vector3(0.062, 0.014, 0.044), Color("#5a2f2a"), Color("#74403a"))
	b.box(Vector3(0, 0.0145, 0.0), Vector3(0.056, 0.003, 0.038), Color("#8a2f2a"))
	# the chronometer in its gimbals: a brass ring with a round white dial inside, and its hinged lid propped open behind it
	b.prism(Vector3(-0.014, 0.0165, 0.0), 0.0135, 0.0135, 0.004, 12, BRASS, BRASS_LIGHT)
	b.prism(Vector3(-0.014, 0.0205, 0.0), 0.0105, 0.0105, 0.0012, 12, PAPER, PAPER)
	b.pole(Vector3(-0.014, 0.0222, 0.0), Vector3(-0.014, 0.0222, 0.0085), 0.0009, INK, 3)
	b.pole(Vector3(-0.014, 0.0222, 0.0), Vector3(-0.0055, 0.0222, 0.0), 0.0009, INK, 3)
	b.box(Vector3(0, 0.032, -0.0215), Vector3(0.062, 0.03, 0.003), Color("#5a2f2a"), Color("#74403a"))
	# a telescope laid beside it, drawn out in two brass sections
	b.pole(Vector3(0.008, 0.019, 0.012), Vector3(0.03, 0.019, 0.012), 0.0036, BRASS, 6)
	b.pole(Vector3(0.0, 0.019, 0.012), Vector3(0.012, 0.019, 0.012), 0.0042, BRASS_LIGHT, 6)
	b.blob(Vector3(0.03, 0.019, 0.012), Vector3(0.0036, 0.0036, 0.0036), 5, 2, GLASS_CLEAR, 0.0)
	return b.commit()
