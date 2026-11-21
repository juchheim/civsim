## Discovery (E6) civic buildings: the botanical garden (FOR-09), the learned society (LRN-09), the exchange house (TRD-11) and the Renaissance Palace
## (BLD-13 Wonder), docs/MVP/39 §4.6 to §4.9.
##
## Not wired to the simulation yet (docs/MVP/39 §9: `discWorks` says what stands and `discWorkState` how it is doing); photographed in the Discovery gallery
## (LowPoly3DView.show_discovery_gallery). Same units as the rest of the kit (1.0 = one tile column, an adult is ~0.1 tall). Buildings face +Z; TAG_CIV faces
## take the owner's colour. Placement is docs/MVP/31a's and docs/MVP/39 §12's: the garden is an outer work outside the wall, the society a small building of
## the Learning hex beside the university, the exchange house a small building of the Market square on the harbor side, the Palace a Wonder on its own hex
## behind the Heart. Each shows its working state on the building (docs/MVP/39 §8.1): lit windows and open doors when staffed, dark glass and shut doors
## when not, so an empty institution never looks alive.
##
##   botanical_garden       a walled formal garden: clipped hedge and brick wall round four geometric beds in two crop tints, a glasshouse leaning on the back
##                          wall, a potting shed, a fountain, tubbed trees and a gate with a leaf sign; a botanist keeps it
##   botanical_garden_idle  no botanist: the gate boarded, the beds bare brown, the glass dark, the fountain dry
##   learned_society        a small classical hall: a portico of four columns and a pediment, a green copper dome on a drum with a brass globe, lit windows
##   learned_society_idle   no scholars: the windows dark, the doors shut
##   exchange_house         a colonnaded loggia open at street level with an upper floor, a tiled roof, a bell cupola and a ship-and-coin sign, lit upper windows
##   exchange_house_idle    no traders: boards across the arches, the upper windows dark
##   renaissance_palace_site   the Wonder's plot: stakes and cord round the footprint, a master mason's lodge, stacked ashlar and a gin
##   renaissance_palace_1..4   foundations and the lodge; the central block's walls up in a scaffold; the wings and the dome shell up with the roof open and
##                             no glass; finished with plate glass, the forecourt garden and the owner's banners
extends RefCounted

const Power := preload("res://Renderer/LowPoly3D/Scripts/art_disc_power.gd")

const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const ASHLAR := Color("#d8cdb0")
const ASHLAR_LIGHT := Color("#ece3c8")
const ASHLAR_DARK := Color("#b0a58a")
const BRICK := Color("#9a4a34")
const BRICK_LIGHT := Color("#b45c42")
const BRICK_DARK := Color("#74382a")
const TILE := Color("#a8492c")
const TILE_DARK := Color("#7f3622")
const SLATE := Color("#58616b")
const COPPER := Color("#6aa08a")
const COPPER_DARK := Color("#4a7e6a")
const BRASS := Color("#c9973c")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const WOOD_DARK := Color("#4a3726")
const DOOR := Color("#2a2119")
const GLASS_DARK := Color("#2d3d4d")
const GLASS_LIT := Color("#e8b45a")
const GLASS_CLEAR := Color("#9cc4d8")
const HEDGE := Color("#3d6e2c")
const HEDGE_DARK := Color("#2f5822")
const HEDGE_LIGHT := Color("#5a8a3a")
const SOIL := Color("#7a5a3c")
const SOIL_DEAD := Color("#8a6f4c")
const BED_A := Color("#4f8a38")
const BED_B := Color("#a05a8a")
const GRAVEL := Color("#c9bb98")
const WATER := Color("#4a82a6")
const WATER_LIGHT := Color("#8fc4ee")
const LANTERN := Color("#ffd27a")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE
const FOLIAGE := M7MeshBuilder.TAG_FOLIAGE
const CROP := M7MeshBuilder.TAG_CROP

static func build(kit) -> void:
	kit.meshes["botanical_garden"] = _garden(kit, true)
	kit.meshes["botanical_garden_idle"] = _garden(kit, false)
	kit.meshes["learned_society"] = _society(kit, true)
	kit.meshes["learned_society_idle"] = _society(kit, false)
	kit.meshes["exchange_house"] = _exchange(kit, true)
	kit.meshes["exchange_house_idle"] = _exchange(kit, false)
	kit.meshes["renaissance_palace_site"] = _palace(kit, 0)
	for stage in [1, 2, 3, 4]:
		var st: int = stage
		kit.meshes["renaissance_palace_%d" % st] = _palace(kit, st)

# ------------------------------------------------------------------ shared pieces

## A window on the +Z face of a wall: a frame, the pane lit or dark, a sill and (optionally) a pediment above it.
static func _window(b: M7MeshBuilder, p: Vector3, w: float, h: float, lit: bool, frame: Color, pediment: bool = false) -> void:
	b.box(p, Vector3(w + 0.006, h + 0.006, 0.003), frame)
	if lit:
		b.tag = EMISSIVE
	b.box(p + Vector3(0, 0, 0.0012), Vector3(w, h, 0.003), GLASS_LIT if lit else GLASS_DARK)
	b.tag = PLAIN
	b.box(p + Vector3(0, -h * 0.5 - 0.003, 0.002), Vector3(w + 0.012, 0.004, 0.006), frame)
	if pediment:
		b.tri(p + Vector3(-w * 0.62, h * 0.5 + 0.004, 0.002), p + Vector3(w * 0.62, h * 0.5 + 0.004, 0.002), p + Vector3(0, h * 0.5 + 0.016, 0.002), frame, p + Vector3(0, 0, -1.0))

## A round-headed opening on the +Z face at z: a dark rectangle with a half-disc head and a stone ring.
static func _arch(b: M7MeshBuilder, cx: float, y0: float, w: float, h: float, z: float, col: Color, ring: Color) -> void:
	b.box(Vector3(cx, y0 + h * 0.5, z), Vector3(w, h, 0.004), col)
	var c := Vector3(cx, y0 + h, z + 0.002)
	for i in 6:
		var a0 := PI * i / 6.0
		var a1 := PI * (i + 1) / 6.0
		b.tri(c, c + Vector3(cos(a0), sin(a0), 0) * w * 0.5, c + Vector3(cos(a1), sin(a1), 0) * w * 0.5, col, c + Vector3(0, 0, -1.0))
		b.pole(c + Vector3(cos(a0), sin(a0), 0) * w * 0.5, c + Vector3(cos(a1), sin(a1), 0) * w * 0.5, 0.0024, ring, 4)

## A round column (fluted look: eight sides) from y0 up h with a square base and capital.
static func _column(b: M7MeshBuilder, p: Vector3, h: float, r: float, col: Color) -> void:
	b.box(p + Vector3(0, 0.003, 0), Vector3(r * 2.7, 0.006, r * 2.7), col.darkened(0.06))
	b.prism(p + Vector3(0, 0.006, 0), r, r * 0.88, h - 0.012, 8, col, col.lightened(0.1))
	b.box(p + Vector3(0, h - 0.003, 0), Vector3(r * 2.8, 0.006, r * 2.8), col.lightened(0.06))

static func _gable_x(b: M7MeshBuilder, c: Vector3, w: float, d: float, h: float, col: Color, col_back: Color, col_end: Color) -> void:
	Power._gable(b, c, w, d, h, col, col_back, col_end)

static func _hip(b: M7MeshBuilder, c: Vector3, w: float, d: float, h: float, col: Color, col_dark: Color) -> void:
	# a four-sided hipped roof: a frustum to a short ridge along X
	var x := w * 0.5 + 0.006
	var z := d * 0.5 + 0.006
	var rx := maxf(x - z, 0.002)
	var below := Vector3(c.x, c.y - 0.5, c.z)
	b.quad(Vector3(c.x - x, c.y, c.z + z), Vector3(c.x + x, c.y, c.z + z), Vector3(c.x + rx, c.y + h, c.z), Vector3(c.x - rx, c.y + h, c.z), col, below)
	b.quad(Vector3(c.x - x, c.y, c.z - z), Vector3(c.x + x, c.y, c.z - z), Vector3(c.x + rx, c.y + h, c.z), Vector3(c.x - rx, c.y + h, c.z), col_dark, below)
	b.tri(Vector3(c.x - x, c.y, c.z + z), Vector3(c.x - x, c.y, c.z - z), Vector3(c.x - rx, c.y + h, c.z), col.darkened(0.06), below)
	b.tri(Vector3(c.x + x, c.y, c.z + z), Vector3(c.x + x, c.y, c.z - z), Vector3(c.x + rx, c.y + h, c.z), col.darkened(0.06), below)

static func _ship_sign(b: M7MeshBuilder, p: Vector3, s: float, col: Color) -> void:
	b.quad(p + Vector3(-1.0, -0.2, 0) * s, p + Vector3(1.0, -0.2, 0) * s, p + Vector3(0.7, -0.55, 0) * s, p + Vector3(-0.7, -0.55, 0) * s, col, p + Vector3(0, 0, -1.0))
	b.tri(p + Vector3(-0.05, -0.15, 0) * s, p + Vector3(-0.05, 1.0, 0) * s, p + Vector3(-0.85, -0.15, 0) * s, col, p + Vector3(0, 0, -1.0))
	b.tri(p + Vector3(0.05, -0.15, 0) * s, p + Vector3(0.05, 0.9, 0) * s, p + Vector3(0.8, -0.15, 0) * s, col, p + Vector3(0, 0, -1.0))

# ------------------------------------------------------------------ botanical garden

static func _garden(kit, staffed: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2300 if staffed else 2301)
	b.tag = PLAIN
	var w := 0.34     # half... the full width (X)
	var d := 0.3      # the full depth (Z)
	# the ground: gravel paths in a cross, the four beds set in it
	b.box(Vector3(0, 0.002, 0), Vector3(w, 0.004, d), GRAVEL)
	var bed_col: Color = SOIL if staffed else SOIL_DEAD
	for sx in [-1.0, 1.0]:
		var x: float = sx
		for sz in [-1.0, 1.0]:
			var z: float = sz
			var bc := Vector3(x * 0.075, 0.0, z * 0.065)
			b.box(bc + Vector3(0, 0.006, 0), Vector3(0.12, 0.008, 0.095), bed_col)
			b.box(bc + Vector3(0, 0.0095, 0), Vector3(0.124, 0.003, 0.099), BRICK_DARK)
			if staffed:
				# rows of planting: two tints per diagonal pair, little rounded plants in lines
				var tint: Color = BED_A if (x * z) > 0.0 else BED_B
				for r in 3:
					for k in 5:
						b.blob(bc + Vector3(-0.044 + 0.022 * k, 0.016, -0.03 + 0.03 * r), Vector3(0.0095, 0.0085, 0.0095), 4, 2, tint, 0.12, tint.darkened(0.2))
	# the wall: brick, round three sides (back and both ends), a clipped hedge along the front with a gate gap
	for sx in [-1.0, 1.0]:
		var x2: float = sx
		b.box(Vector3(x2 * (w * 0.5 - 0.004), 0.03, 0.0), Vector3(0.008, 0.06, d), BRICK, BRICK_LIGHT)
	b.box(Vector3(0, 0.03, -d * 0.5 + 0.004), Vector3(w, 0.06, 0.008), BRICK, BRICK_LIGHT)
	b.box(Vector3(0, 0.062, -d * 0.5 + 0.004), Vector3(w + 0.006, 0.005, 0.014), STONE_LIGHT)
	for k in 7:
		var hx := lerpf(-w * 0.5 + 0.02, w * 0.5 - 0.02, float(k) / 6.0)
		if absf(hx) < 0.032:
			continue
		b.box(Vector3(hx, 0.018, d * 0.5 - 0.006), Vector3(0.042, 0.036, 0.016), HEDGE if k % 2 == 0 else HEDGE_DARK, HEDGE_LIGHT)
	# the gate: two stone piers with ball finials, a wrought-iron gate that stands open when staffed, boarded when not; a leaf sign over it
	for sx in [-1.0, 1.0]:
		var x3: float = sx
		b.box(Vector3(x3 * 0.032, 0.03, d * 0.5 - 0.006), Vector3(0.014, 0.06, 0.014), STONE_LIGHT, STONE)
		b.blob(Vector3(x3 * 0.032, 0.066, d * 0.5 - 0.006), Vector3(0.0075, 0.0075, 0.0075), 5, 2, STONE_LIGHT, 0.0)
	if staffed:
		b.pole(Vector3(-0.025, 0.003, d * 0.5 - 0.006), Vector3(-0.025, 0.05, d * 0.5 + 0.02), 0.0024, DOOR, 4)
		b.pole(Vector3(0.025, 0.003, d * 0.5 - 0.006), Vector3(0.025, 0.05, d * 0.5 + 0.02), 0.0024, DOOR, 4)
	else:
		b.box(Vector3(0, 0.03, d * 0.5 - 0.006), Vector3(0.05, 0.056, 0.005), WOOD_DARK, WOOD)
		b.pole(Vector3(-0.024, 0.014, d * 0.5 - 0.0025), Vector3(0.024, 0.05, d * 0.5 - 0.0025), 0.0022, WOOD_LIGHT, 4)
	var sign_p := Vector3(0, 0.078, d * 0.5 - 0.006)
	b.box(sign_p, Vector3(0.026, 0.026, 0.004), STONE_DARK)
	# a leaf: a lozenge in green with a mid-rib
	b.quad(sign_p + Vector3(0, 0.011, 0.0025), sign_p + Vector3(0.0085, 0.0, 0.0025), sign_p + Vector3(0, -0.011, 0.0025), sign_p + Vector3(-0.0085, 0.0, 0.0025), BED_A, sign_p + Vector3(0, 0, -1.0))
	b.pole(sign_p + Vector3(0, 0.01, 0.0035), sign_p + Vector3(0, -0.01, 0.0035), 0.0008, HEDGE_DARK, 3)
	# the glasshouse leaning on the back wall: a timber frame with a sloped glass roof and glass ends
	var gh := Vector3(0.0, 0.0, -d * 0.5 + 0.045)
	b.box(gh + Vector3(0, 0.003, 0), Vector3(0.2, 0.006, 0.07), STONE_DARK, STONE)
	var gcol: Color = GLASS_CLEAR if staffed else GLASS_DARK
	var top := Vector3(0, 0.07, -d * 0.5 + 0.012)
	b.quad(gh + Vector3(-0.1, 0.006, 0.035), gh + Vector3(0.1, 0.006, 0.035), Vector3(0.1, 0.07, top.z), Vector3(-0.1, 0.07, top.z), gcol, Vector3(0, 0.03, 1.0))
	b.tri(gh + Vector3(-0.1, 0.006, 0.035), gh + Vector3(-0.1, 0.006, -0.03), Vector3(-0.1, 0.07, top.z), gcol, Vector3(0.0, 0.03, 0.0))
	b.tri(gh + Vector3(0.1, 0.006, 0.035), gh + Vector3(0.1, 0.006, -0.03), Vector3(0.1, 0.07, top.z), gcol, Vector3(0.0, 0.03, 0.0))
	for k in 6:
		var mx := lerpf(-0.1, 0.1, float(k) / 5.0)
		b.pole(gh + Vector3(mx, 0.006, 0.035), Vector3(mx, 0.07, top.z), 0.0016, WOOD, 4)
	b.pole(gh + Vector3(-0.1, 0.03, 0.0125), gh + Vector3(0.1, 0.03, 0.0125), 0.0015, WOOD, 4)
	if staffed:
		# plants seen through the glass
		for k in 5:
			b.blob(gh + Vector3(-0.07 + 0.035 * k, 0.018, 0.0), Vector3(0.011, 0.014, 0.011), 4, 2, HEDGE_LIGHT, 0.1)
	# the potting shed in the right corner
	b.box(Vector3(0.135, 0.02, -0.075), Vector3(0.05, 0.04, 0.04), WOOD, WOOD_LIGHT)
	_gable_x(b, Vector3(0.135, 0.04, -0.075), 0.05, 0.04, 0.02, TILE, TILE_DARK, WOOD_DARK)
	b.box(Vector3(0.135, 0.016, -0.0545), Vector3(0.014, 0.028, 0.003), DOOR)
	# the fountain in the middle of the cross: a stone basin, a column and a jet (dry when unstaffed)
	b.prism(Vector3(0, 0.004, 0.0), 0.034, 0.03, 0.014, 10, STONE_LIGHT, STONE)
	b.prism(Vector3(0, 0.017, 0.0), 0.028, 0.028, 0.002, 10, WATER if staffed else STONE_DARK, WATER_LIGHT if staffed else STONE_DARK)
	b.prism(Vector3(0, 0.014, 0.0), 0.006, 0.004, 0.026, 6, STONE_LIGHT, STONE)
	if staffed:
		b.tri(Vector3(0, 0.046, 0), Vector3(-0.004, 0.026, 0.003), Vector3(0.004, 0.026, -0.003), WATER_LIGHT, Vector3(0, 0.03, 0.2))
	# tubbed trees at the front corners
	for sx in [-1.0, 1.0]:
		var x4: float = sx
		b.prism(Vector3(x4 * 0.15, 0.0, 0.1), 0.012, 0.01, 0.018, 6, WOOD, WOOD_LIGHT)
		b.blob(Vector3(x4 * 0.15, 0.04, 0.1), Vector3(0.018, 0.02, 0.018), 5, 3, HEDGE_LIGHT if staffed else Color("#8a8a4a"), 0.14, HEDGE)
	return b.commit()

# ------------------------------------------------------------------ learned society

static func _society(kit, staffed: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2310 if staffed else 2311)
	b.tag = PLAIN
	var w := 0.16
	var d := 0.12
	var h := 0.08
	# a stepped plinth, the hall body in pale ashlar with a string course, the windows in two storeys
	b.box(Vector3(0, 0.004, 0.0), Vector3(w + 0.03, 0.008, d + 0.05), STONE_DARK, STONE)
	b.box(Vector3(0, 0.008 + h * 0.5, 0.0), Vector3(w, h, d), ASHLAR, ASHLAR_LIGHT)
	b.box(Vector3(0, 0.008 + h * 0.5, d * 0.5 + 0.0015), Vector3(w + 0.008, 0.005, 0.004), ASHLAR_LIGHT)
	b.box(Vector3(0, 0.008 + h, 0.0), Vector3(w + 0.012, 0.008, d + 0.012), ASHLAR_LIGHT, ASHLAR)
	for k in 4:
		var wx := -0.06 + 0.04 * k
		_window(b, Vector3(wx, 0.034, d * 0.5 + 0.0016), 0.014, 0.026, staffed, STONE_LIGHT, true)
	for sx in [-0.06, 0.06]:
		var x: float = sx
		_window(b, Vector3(x, 0.07, d * 0.5 + 0.0016), 0.012, 0.016, staffed, STONE_LIGHT)
	# the portico: four columns carrying a pediment, in front of the middle windows, and the door behind them
	var pz := d * 0.5 + 0.035
	for k in 4:
		var cx := -0.045 + 0.03 * k
		_column(b, Vector3(cx, 0.008, pz), h - 0.004, 0.0068, ASHLAR_LIGHT)
	b.box(Vector3(0, 0.008 + h, pz - 0.005), Vector3(0.115, 0.008, 0.07), ASHLAR_LIGHT, ASHLAR)
	b.tri(Vector3(-0.0625, 0.016 + h, pz + 0.0295), Vector3(0.0625, 0.016 + h, pz + 0.0295), Vector3(0, 0.016 + h + 0.024, pz + 0.0295), ASHLAR_LIGHT, Vector3(0, 0.1, pz - 1.0))
	b.tri(Vector3(-0.0545, 0.0175 + h, pz + 0.0315), Vector3(0.0545, 0.0175 + h, pz + 0.0315), Vector3(0, 0.0175 + h + 0.0195, pz + 0.0315), ASHLAR_DARK, Vector3(0, 0.1, pz - 1.0))
	b.box(Vector3(0, 0.008 + 0.0004, pz - 0.005), Vector3(0.12, 0.003, 0.074), STONE_LIGHT)
	b.box(Vector3(0, 0.024, d * 0.5 + 0.0018), Vector3(0.02, 0.032, 0.003), DOOR if not staffed else WOOD_DARK)
	if staffed:
		b.tag = EMISSIVE
		b.box(Vector3(0, 0.0245, d * 0.5 + 0.0033), Vector3(0.014, 0.026, 0.0015), GLASS_LIT)
		b.tag = PLAIN
	# the roof: a low pavilion hidden behind a balustrade, and on it a drum carrying the copper dome with a lantern and a brass globe on a ring
	b.box(Vector3(0, 0.016 + h, 0.0), Vector3(w - 0.01, 0.006, d - 0.01), SLATE)
	b.prism(Vector3(0, 0.016 + h, 0.0), 0.034, 0.034, 0.026, 12, ASHLAR, ASHLAR_LIGHT)
	for k in 8:
		var a := TAU * k / 8.0
		b.box(Vector3(cos(a) * 0.0345, 0.016 + h + 0.014, sin(a) * 0.0345), Vector3(0.007, 0.012, 0.007), GLASS_DARK if not staffed else GLASS_LIT)
	b.blob(Vector3(0, 0.042 + h, 0.0), Vector3(0.034, 0.034, 0.034), 10, 4, COPPER, 0.0, COPPER_DARK)
	b.prism(Vector3(0, 0.07 + h, 0.0), 0.008, 0.006, 0.01, 6, ASHLAR_LIGHT, ASHLAR)
	b.blob(Vector3(0, 0.086 + h, 0.0), Vector3(0.009, 0.009, 0.009), 6, 3, BRASS, 0.0)
	b.pole(Vector3(-0.012, 0.086 + h, 0.0), Vector3(0.012, 0.086 + h, 0.0), 0.0012, BRASS, 3)
	b.pole(Vector3(0, 0.086 + h, -0.012), Vector3(0, 0.086 + h, 0.012), 0.0012, BRASS, 3)
	# balustrade posts along the eaves
	for k in 9:
		b.box(Vector3(-0.07 + 0.0175 * k, 0.012 + h, d * 0.5 + 0.002), Vector3(0.004, 0.008, 0.004), ASHLAR_LIGHT)
	return b.commit()

# ------------------------------------------------------------------ exchange house

static func _exchange(kit, staffed: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2320 if staffed else 2321)
	b.tag = PLAIN
	var w := 0.2
	var d := 0.1
	var gh := 0.045    # the ground storey
	b.box(Vector3(0, 0.003, 0), Vector3(w + 0.02, 0.006, d + 0.03), STONE_DARK, STONE)
	# the ground storey is an open loggia: a back wall and a row of piers carrying round arches, five bays
	b.box(Vector3(0, 0.006 + gh * 0.5, -d * 0.5 + 0.004), Vector3(w, gh, 0.008), BRICK, BRICK_LIGHT)
	b.box(Vector3(0, 0.006 + gh * 0.5, 0.0), Vector3(w, gh, d), BRICK_DARK)   # the interior shade seen through the arches
	var bays := 5
	for k in bays + 1:
		var px := lerpf(-w * 0.5 + 0.007, w * 0.5 - 0.007, float(k) / bays)
		b.box(Vector3(px, 0.006 + gh * 0.5, d * 0.5 - 0.005), Vector3(0.014, gh, 0.014), ASHLAR, ASHLAR_LIGHT)
	for k in bays:
		var cx := lerpf(-w * 0.5 + 0.007, w * 0.5 - 0.007, (float(k) + 0.5) / bays)
		var bw := (w - 0.014) / bays - 0.014
		_arch(b, cx, 0.006, bw, gh - 0.016, d * 0.5 + 0.0015, DOOR, ASHLAR_LIGHT)
		if not staffed:
			# boards nailed across the opening
			b.box(Vector3(cx, 0.006 + (gh - 0.016) * 0.5, d * 0.5 + 0.004), Vector3(bw + 0.004, gh - 0.014, 0.003), WOOD_DARK, WOOD)
			b.pole(Vector3(cx - bw * 0.5, 0.012, d * 0.5 + 0.0065), Vector3(cx + bw * 0.5, gh - 0.01, d * 0.5 + 0.0065), 0.0022, WOOD_LIGHT, 4)
	# the cornice, then the upper storey in ashlar with a row of windows and a balustrade
	b.box(Vector3(0, 0.006 + gh + 0.003, 0.0), Vector3(w + 0.012, 0.007, d + 0.012), ASHLAR_LIGHT, ASHLAR)
	var uh := 0.04
	b.box(Vector3(0, 0.006 + gh + 0.006 + uh * 0.5, 0.0), Vector3(w - 0.006, uh, d - 0.006), ASHLAR, ASHLAR_LIGHT)
	for k in 5:
		var wx := lerpf(-w * 0.5 + 0.02, w * 0.5 - 0.02, float(k) / 4.0)
		_window(b, Vector3(wx, 0.006 + gh + 0.006 + uh * 0.55, d * 0.5 - 0.0028), 0.014, 0.022, staffed, ASHLAR_LIGHT, k % 2 == 0)
	b.box(Vector3(0, 0.006 + gh + 0.006 + uh + 0.003, 0.0), Vector3(w + 0.006, 0.006, d + 0.006), ASHLAR_LIGHT, ASHLAR)
	# the tiled hipped roof and a bell cupola on the ridge
	_hip(b, Vector3(0, 0.006 + gh + 0.006 + uh + 0.006, 0.0), w - 0.002, d - 0.002, 0.028, TILE, TILE_DARK)
	var cb := Vector3(0, 0.006 + gh + 0.006 + uh + 0.034, 0.0)
	b.box(cb + Vector3(0, 0.007, 0), Vector3(0.026, 0.014, 0.026), ASHLAR, ASHLAR_LIGHT)
	for sx in [-1.0, 1.0]:
		var x: float = sx
		b.box(cb + Vector3(x * 0.007, 0.011, 0.0138), Vector3(0.008, 0.012, 0.0015), DOOR)
	b.prism(cb + Vector3(0, 0.014, 0), 0.02, 0.0, 0.022, 4, COPPER, COPPER_DARK, PI * 0.25)
	b.blob(cb + Vector3(0, 0.041, 0), Vector3(0.0045, 0.0045, 0.0045), 4, 2, BRASS, 0.0)
	b.blob(cb + Vector3(0, 0.0075, 0.0), Vector3(0.0055, 0.0065, 0.0055), 5, 2, BRASS, 0.0)
	# the ship-and-coin sign hung over the middle bay: a board on an iron bracket
	var sp := Vector3(0.0, 0.006 + gh + 0.0, d * 0.5 + 0.034)
	b.pole(Vector3(0, 0.006 + gh - 0.002, d * 0.5 + 0.006), sp + Vector3(0, -0.002, 0), 0.0016, DOOR, 4)
	b.box(sp + Vector3(0, -0.011, 0), Vector3(0.026, 0.018, 0.003), STONE_DARK)
	_ship_sign(b, sp + Vector3(-0.004, -0.0095, 0.002), 0.0058, GLASS_LIT)
	b.blob(sp + Vector3(0.0085, -0.014, 0.0022), Vector3(0.0034, 0.0034, 0.0012), 6, 2, BRASS, 0.0)
	# the owner's banner from a pole at the right end of the roof (the owner's colour on the cloth only)
	b.pole(Vector3(w * 0.5 - 0.016, 0.006 + gh + 0.006 + uh + 0.006, -0.02), Vector3(w * 0.5 - 0.016, 0.006 + gh + 0.006 + uh + 0.056, -0.02), 0.0016, WOOD, 4)
	b.tag = CIV
	var fy := 0.006 + gh + 0.006 + uh + 0.054
	b.quad(Vector3(w * 0.5 - 0.016, fy, -0.02), Vector3(w * 0.5 + 0.014, fy - 0.003, -0.02), Vector3(w * 0.5 + 0.014, fy - 0.017, -0.02), Vector3(w * 0.5 - 0.016, fy - 0.014, -0.02), Color.WHITE, Vector3(w * 0.5, fy - 0.01, -1.0))
	b.quad(Vector3(w * 0.5 - 0.016, fy, -0.02), Vector3(w * 0.5 + 0.014, fy - 0.003, -0.02), Vector3(w * 0.5 + 0.014, fy - 0.017, -0.02), Vector3(w * 0.5 - 0.016, fy - 0.014, -0.02), Color.WHITE, Vector3(w * 0.5, fy - 0.01, 1.0))
	b.tag = PLAIN
	# bales and barrels under the arcade at the front corner, only while it trades
	if staffed:
		b.blob(Vector3(-0.085, 0.016, d * 0.5 + 0.018), Vector3(0.011, 0.01, 0.01), 5, 2, Color("#cdbf9f"), 0.08, Color("#b9a982"))
		b.prism(Vector3(0.085, 0.006, d * 0.5 + 0.016), 0.008, 0.008, 0.015, 8, WOOD_LIGHT, WOOD_LIGHT.lightened(0.1))
	return b.commit()

# ------------------------------------------------------------------ Renaissance Palace (Wonder)

static func _palace(kit, stage: int) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2330 + stage)
	b.tag = PLAIN
	# the plot: a gravel forecourt over the whole hex footprint (a 0.5 half-width block)
	b.box(Vector3(0, 0.003, 0.0), Vector3(1.0, 0.006, 0.84), GRAVEL.darkened(0.06))
	if stage == 0:
		_palace_site(b)
		return b.commit()
	# footprint: the central block 0.34 wide (X) by 0.24 deep, the wings 0.26 wide either side, set back; the forecourt in front toward +Z
	var cw := 0.34
	var cd := 0.24
	var ch := 0.17
	var ww := 0.26
	var wd := 0.18
	var wh := 0.12
	var cz := -0.1
	# the foundations: a stone plinth under everything
	b.box(Vector3(0, 0.014, cz), Vector3(cw + 0.026 + ww * 2.0, 0.016, cd + 0.026), STONE_DARK, STONE)
	if stage == 1:
		_lodge(b, Vector3(0.44, 0.0, 0.1))
		for k in 4:
			b.box(Vector3(-0.38 + 0.002 * k, 0.026 + 0.014 * k, 0.16), Vector3(0.1, 0.014, 0.05), ASHLAR, ASHLAR_LIGHT)
		_gin(b, Vector3(-0.1, 0.0, 0.1))
		# the first courses of the walls
		b.box(Vector3(0, 0.03, cz + cd * 0.5 - 0.008), Vector3(cw, 0.028, 0.016), ASHLAR, ASHLAR_LIGHT)
		b.box(Vector3(0, 0.03, cz - cd * 0.5 + 0.008), Vector3(cw, 0.028, 0.016), ASHLAR, ASHLAR_LIGHT)
		b.box(Vector3(-cw * 0.5 + 0.008, 0.03, cz), Vector3(0.016, 0.028, cd), ASHLAR, ASHLAR_LIGHT)
		b.box(Vector3(cw * 0.5 - 0.008, 0.03, cz), Vector3(0.016, 0.028, cd), ASHLAR, ASHLAR_LIGHT)
		return b.commit()
	# the central block: ashlar walls rising (stage 2 stops short of its full height inside a scaffold)
	var blockh := ch if stage >= 3 else ch * 0.62
	b.box(Vector3(0, 0.022 + blockh * 0.5, cz), Vector3(cw, blockh, cd), ASHLAR, ASHLAR_LIGHT)
	if stage == 2:
		_lodge(b, Vector3(0.5, 0.0, 0.12))
		_scaffold(b, Vector3(0, 0.0, cz + cd * 0.5 + 0.018), cw, 0.12)
		_gin(b, Vector3(-0.3, 0.0, 0.16))
		for k in 3:
			b.box(Vector3(0.35 + 0.002 * k, 0.026 + 0.014 * k, 0.2), Vector3(0.09, 0.014, 0.05), ASHLAR, ASHLAR_LIGHT)
		return b.commit()
	# wings (stage 3: walls up and roof open on the right; stage 4 finished)
	for sx in [-1.0, 1.0]:
		var x: float = sx
		var wc := Vector3(x * (cw * 0.5 + ww * 0.5 + 0.004), 0.0, cz + 0.03)
		b.box(wc + Vector3(0, 0.022 + wh * 0.5, 0), Vector3(ww, wh, wd), ASHLAR, ASHLAR_LIGHT)
		b.box(wc + Vector3(0, 0.022 + wh + 0.003, 0), Vector3(ww + 0.008, 0.006, wd + 0.008), ASHLAR_LIGHT, ASHLAR)
		if stage == 4 or x < 0.0:
			Power._gable(b, wc + Vector3(0, 0.022 + wh + 0.006, 0), ww, wd, 0.05, SLATE, SLATE.darkened(0.1), ASHLAR_DARK)
		else:
			# the open roof frame: rafters on the right wing, still being slated
			for k in 6:
				var rx := -ww * 0.5 + 0.02 + 0.044 * k
				b.pole(wc + Vector3(rx, 0.022 + wh + 0.006, wd * 0.5), wc + Vector3(rx, 0.022 + wh + 0.056, 0.0), 0.0026, WOOD_LIGHT, 4)
				b.pole(wc + Vector3(rx, 0.022 + wh + 0.006, -wd * 0.5), wc + Vector3(rx, 0.022 + wh + 0.056, 0.0), 0.0026, WOOD_LIGHT, 4)
		# window grid on the wing's +Z face
		for r in 2:
			for k in 4:
				var wx2 := wc.x - ww * 0.5 + 0.034 + 0.064 * k
				_window(b, Vector3(wx2, 0.022 + 0.032 + 0.05 * r, wc.z + wd * 0.5 + 0.0016), 0.018, 0.03, stage == 4, ASHLAR_LIGHT, r == 1)
	# the central block's face: two storeys of windows, a portico at the middle with columns and a pediment, a balustrade
	var face_z := cz + cd * 0.5 + 0.0016
	for r in 2:
		for k in 7:
			if k == 3 and r == 0:
				continue
			var wx3 := -cw * 0.5 + 0.034 + 0.045 * k
			_window(b, Vector3(wx3, 0.022 + 0.04 + 0.062 * r, face_z), 0.02, 0.036, stage == 4, ASHLAR_LIGHT, r == 1 and k % 2 == 0)
	b.box(Vector3(0, 0.022 + 0.035, face_z + 0.0006), Vector3(0.034, 0.05, 0.003), DOOR)
	for k in 4:
		_column(b, Vector3(-0.066 + 0.044 * k, 0.022, face_z + 0.04), 0.1, 0.0085, ASHLAR_LIGHT)
	b.box(Vector3(0, 0.022 + 0.1, face_z + 0.02), Vector3(0.17, 0.01, 0.05), ASHLAR_LIGHT, ASHLAR)
	b.tri(Vector3(-0.088, 0.027 + 0.1, face_z + 0.044), Vector3(0.088, 0.027 + 0.1, face_z + 0.044), Vector3(0, 0.027 + 0.1 + 0.03, face_z + 0.044), ASHLAR_LIGHT, Vector3(0, 0.14, face_z - 1.0))
	b.box(Vector3(0, 0.022 + blockh + 0.004, cz), Vector3(cw + 0.01, 0.008, cd + 0.01), ASHLAR_LIGHT, ASHLAR)
	# the dome: a drum over the middle of the block with windows, a copper-lead dome and a lantern with a banner pole (stage 3 has the drum and the dome frame only)
	var dc := Vector3(0, 0.022 + blockh + 0.008, cz)
	b.prism(dc, 0.092, 0.088, 0.05, 14, ASHLAR, ASHLAR_LIGHT)
	for k in 10:
		var a := TAU * k / 10.0
		b.box(dc + Vector3(cos(a) * 0.09, 0.028, sin(a) * 0.09), Vector3(0.012, 0.022, 0.012), GLASS_LIT if stage == 4 else GLASS_DARK)
	if stage == 3:
		# the dome frame: ribs of timber on the drum, no skin yet
		for k in 8:
			var a2 := TAU * k / 8.0
			b.pole(dc + Vector3(cos(a2) * 0.088, 0.05, sin(a2) * 0.088), dc + Vector3(0, 0.15, 0), 0.0028, WOOD_LIGHT, 4)
		_scaffold(b, Vector3(0, 0.0, cz + cd * 0.5 + 0.02), cw, 0.16)
		_gin(b, Vector3(-0.5, 0.0, 0.1))
		_lodge(b, Vector3(0.62, 0.0, 0.14))
		return b.commit()
	b.blob(dc + Vector3(0, 0.05, 0), Vector3(0.088, 0.1, 0.088), 12, 5, COPPER, 0.0, COPPER_DARK)
	b.prism(dc + Vector3(0, 0.145, 0), 0.016, 0.012, 0.03, 8, ASHLAR_LIGHT, ASHLAR)
	b.prism(dc + Vector3(0, 0.175, 0), 0.016, 0.0, 0.02, 8, COPPER, COPPER_DARK)
	b.pole(dc + Vector3(0, 0.19, 0), dc + Vector3(0, 0.27, 0), 0.0024, WOOD, 4)
	# the owner's banner on the lantern pole
	b.tag = CIV
	var fy2 := dc.y + 0.26
	for face_hint in [-1.0, 1.0]:
		var fh: float = face_hint
		b.quad(dc + Vector3(0, 0.27, 0), dc + Vector3(0.07, 0.264, 0), dc + Vector3(0.07, 0.232, 0), dc + Vector3(0, 0.238, 0), Color.WHITE, dc + Vector3(0.03, 0.25, fh))
	b.tag = PLAIN
	# the forecourt: clipped hedges in two rows, a round basin with a jet, a gravel path to the door, a flagpole at each end of the wings
	for sx in [-1.0, 1.0]:
		var x2: float = sx
		for k in 3:
			b.box(Vector3(x2 * (0.14 + 0.1 * k), 0.016, 0.14), Vector3(0.07, 0.024, 0.02), HEDGE if k % 2 == 0 else HEDGE_DARK, HEDGE_LIGHT)
			b.box(Vector3(x2 * (0.14 + 0.1 * k), 0.016, 0.2), Vector3(0.07, 0.024, 0.02), HEDGE_DARK if k % 2 == 0 else HEDGE, HEDGE_LIGHT)
	b.prism(Vector3(0, 0.006, 0.17), 0.045, 0.04, 0.012, 12, STONE_LIGHT, STONE)
	b.prism(Vector3(0, 0.018, 0.17), 0.038, 0.038, 0.002, 12, WATER, WATER_LIGHT)
	b.prism(Vector3(0, 0.014, 0.17), 0.006, 0.004, 0.026, 6, STONE_LIGHT, STONE)
	b.box(Vector3(0, 0.008, 0.06), Vector3(0.05, 0.003, 0.15), GRAVEL.lightened(0.08))
	for sx in [-1.0, 1.0]:
		var x3: float = sx
		b.pole(Vector3(x3 * 0.42, 0.012, -0.05), Vector3(x3 * 0.42, 0.2, -0.05), 0.0026, WOOD, 4)
		b.tag = CIV
		b.quad(Vector3(x3 * 0.42, 0.2, -0.05), Vector3(x3 * 0.42 + x3 * 0.045, 0.197, -0.05), Vector3(x3 * 0.42 + x3 * 0.045, 0.176, -0.05), Vector3(x3 * 0.42, 0.18, -0.05), Color.WHITE, Vector3(x3 * 0.44, 0.19, -1.0))
		b.quad(Vector3(x3 * 0.42, 0.2, -0.05), Vector3(x3 * 0.42 + x3 * 0.045, 0.197, -0.05), Vector3(x3 * 0.42 + x3 * 0.045, 0.176, -0.05), Vector3(x3 * 0.42, 0.18, -0.05), Color.WHITE, Vector3(x3 * 0.44, 0.19, 1.0))
		b.tag = PLAIN
	return b.commit()

static func _palace_site(b: M7MeshBuilder) -> void:
	# stakes and a cord round the footprint, a lodge, ashlar stacked ready, a gin
	var pts := [Vector3(-0.42, 0, -0.22), Vector3(0.42, 0, -0.22), Vector3(0.42, 0, 0.02), Vector3(-0.42, 0, 0.02)]
	for i in 4:
		var a: Vector3 = pts[i]
		var e: Vector3 = pts[(i + 1) % 4]
		b.pole(a, a + Vector3(0, 0.04, 0), 0.0028, WOOD_LIGHT, 4)
		b.pole(a + Vector3(0, 0.034, 0), e + Vector3(0, 0.034, 0), 0.0011, Color("#b9a68a"), 3)
	_lodge(b, Vector3(0.46, 0.0, 0.14))
	_gin(b, Vector3(-0.1, 0.0, 0.14))
	for k in 5:
		b.box(Vector3(-0.38 + 0.002 * k, 0.012 + 0.014 * (k % 3), 0.1 + 0.012 * (k / 3)), Vector3(0.09, 0.014, 0.05), ASHLAR, ASHLAR_LIGHT)
	b.box(Vector3(0.0, 0.01, 0.2), Vector3(0.14, 0.012, 0.03), WOOD_LIGHT, WOOD)

static func _lodge(b: M7MeshBuilder, p: Vector3) -> void:
	# the master mason's lodge: a plank shed with a lean-to roof and a door
	b.box(p + Vector3(0, 0.022, 0), Vector3(0.1, 0.044, 0.07), WOOD, WOOD_LIGHT)
	Power._gable(b, p + Vector3(0, 0.044, 0), 0.1, 0.07, 0.02, TILE, TILE_DARK, WOOD_DARK)
	b.box(p + Vector3(0, 0.016, 0.0365), Vector3(0.018, 0.028, 0.003), DOOR)
	b.box(p + Vector3(0.03, 0.024, 0.0365), Vector3(0.012, 0.012, 0.003), GLASS_LIT)

static func _gin(b: M7MeshBuilder, p: Vector3) -> void:
	# a gin: a tripod of poles carrying a pulley with a block hanging from it
	var top := p + Vector3(0, 0.15, 0)
	b.pole(p + Vector3(-0.04, 0, 0.03), top, 0.0038, WOOD, 4)
	b.pole(p + Vector3(0.04, 0, 0.03), top, 0.0038, WOOD, 4)
	b.pole(p + Vector3(0, 0, -0.045), top, 0.0038, WOOD, 4)
	b.pole(top, top + Vector3(0.0, -0.07, 0.0), 0.0012, Color("#b9a68a"), 3)
	b.box(top + Vector3(0.0, -0.08, 0.0), Vector3(0.018, 0.012, 0.012), ASHLAR, ASHLAR_LIGHT)

static func _scaffold(b: M7MeshBuilder, p: Vector3, w: float, h: float) -> void:
	for k in 6:
		var x := p.x - w * 0.5 + w * k / 5.0
		b.pole(Vector3(x, 0.0, p.z), Vector3(x, h, p.z), 0.0032, WOOD, 4)
	for y in [0.04, 0.08, 0.12]:
		var yy: float = y
		if yy > h:
			continue
		b.pole(Vector3(p.x - w * 0.5, yy, p.z), Vector3(p.x + w * 0.5, yy, p.z), 0.0032, WOOD_LIGHT, 4)
	b.box(Vector3(p.x, 0.082 if h > 0.08 else h * 0.6, p.z), Vector3(w * 0.9, 0.004, 0.026), WOOD_LIGHT, WOOD_LIGHT.lightened(0.08))
