## The basic structures of the early ages that the settlement can raise on top of its houses: the totem (BEL-02), the marker or cairn (TER-03), the pilgrimage
## shrine (BEL-05), the signal post (COM-04) and the potter's workshop (CER-04). docs/MVP/29 lists what each one does in the simulation.
##
## Laid out by world_builder.gd (`_settlements`: the totem and the workshop are two more yard items on the hearth's spiral, the signal post stands at the
## village edge, the shrine and any marker without a camp stand outside it, `_camps`: a marker on each seasonal camp). Same units as the rest of the kit
## (1.0 = one tile column, an adult is ~0.1 tall, a hut ~0.15) and drawn at village scale like the other yard pieces. Fronts face +Z; TAG_CIV faces take
## the owner's colour, TAG_EMISSIVE glows.
##
##   totem             a tall carved pole on a ring of stones: three stacked faces with painted beaks, a civ-coloured band and a bird with spread wings on top
##   marker            a cairn of flat stones stacked in tiers, a pale capstone and a leaning stake with a civ-coloured rag
##   shrine            a small grey stone shrine on three steps: a corbelled slab roof over a dark cell with a lit lamp, standing stones with prayer ribbons, offering piles
##   signal_post       a tall braced timber mast with a platform, an iron brazier burning on top, a signal drum hung below it and a civ pennant (no roof: the lookout has one)
##   potters_workshop  a timber-framed clay-walled shed with a lean-to thatch roof, open to the front: a potter's wheel and stool, a shelf of jars, a small kiln beside it
extends RefCounted

const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const BARK := Color("#4b3a2a")
const THATCH := Color("#c29a52")
const THATCH_DARK := Color("#a07c3e")
const STONE := Color("#8f8a80")
const STONE_LIGHT := Color("#b3ad9f")
const STONE_DARK := Color("#6c675f")
const PALE_STONE := Color("#c4beb2")
const MOSS := Color("#6f7a52")
const EARTH := Color("#7b6247")
const DARK := Color("#2a2119")
const SOOT := Color("#2b221c")
const CREAM := Color("#f3ead6")
const OCHRE := Color("#a8432a")
const GOLD := Color("#c9973c")
const CLAY := Color("#b5764a")
const CLAY_DARK := Color("#8c5636")
const CLAY_WALL := Color("#cfa57a")
const IRON := Color("#3f444c")
const ROPE := Color("#cdb57e")
const EMBER := Color("#ff6a20")
const EMBER_HI := Color("#ffb347")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const GLOW := M7MeshBuilder.TAG_EMISSIVE

static func build(kit) -> void:
	kit.meshes["totem"] = _totem(kit)
	kit.meshes["marker"] = _marker(kit)
	kit.meshes["shrine"] = _shrine(kit)
	kit.meshes["signal_post"] = _signal_post(kit)
	kit.meshes["potters_workshop"] = _workshop(kit)

# ------------------------------------------------------------------ helpers

## Moves the next parts to p, turned by yaw (about Y), tilted by tilt (about X: + leans the top toward +Z) and rolled (about Z).
static func _at(b: M7MeshBuilder, p: Vector3, yaw: float = 0.0, tilt: float = 0.0, roll: float = 0.0) -> void:
	b.xf = Transform3D(Basis.from_euler(Vector3(tilt, yaw, roll)), p)

static func _home(b: M7MeshBuilder) -> void:
	b.xf = Transform3D.IDENTITY

## A squat jar: a belly and a short neck, `s` times the base size.
static func _jar(b: M7MeshBuilder, p: Vector3, s: float, col: Color) -> void:
	b.prism(p, 0.009 * s, 0.014 * s, 0.013 * s, 5, col.darkened(0.1), col)
	b.prism(p + Vector3(0, 0.013 * s, 0), 0.014 * s, 0.009 * s, 0.01 * s, 5, col, DARK)

# ------------------------------------------------------------------ totem

static func _totem(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1040)
	b.tag = PLAIN
	# a ring of footing stones round the foot of the pole
	for i in 6:
		var a := TAU * i / 6.0 + 0.3
		b.blob(Vector3(cos(a) * 0.03, 0.007, sin(a) * 0.03), Vector3(0.016, 0.01, 0.014), 5, 2, STONE_LIGHT if i % 2 == 0 else STONE, 0.15, STONE_DARK)
	# the pole: a debarked trunk, thinner toward the top
	b.prism(Vector3.ZERO, 0.02, 0.016, 0.23, 6, WOOD_LIGHT, WOOD, 0.2)
	# three carved faces, one above the other: a red one at the foot, a pale one, and a soot-black one below the bird; each has a brow, eyes and a beak
	var faces := [[0.035, OCHRE], [0.091, CREAM], [0.147, SOOT]]
	for f in faces:
		var y: float = f[0]
		var col: Color = f[1]
		b.box(Vector3(0, y + 0.026, 0), Vector3(0.044, 0.048, 0.044), col, col.lightened(0.08))
		b.box(Vector3(0, y + 0.037, 0.0235), Vector3(0.036, 0.006, 0.004), SOOT if col != SOOT else CREAM)
		for sx in [-0.011, 0.011]:
			b.box(Vector3(sx, y + 0.028, 0.0235), Vector3(0.009, 0.009, 0.004), SOOT if col != SOOT else CREAM)
		# the beak, hooked down
		b.box(Vector3(0, y + 0.015, 0.033), Vector3(0.013, 0.012, 0.02), GOLD)
		b.box(Vector3(0, y + 0.006, 0.039), Vector3(0.011, 0.01, 0.008), GOLD.darkened(0.2))
	# the owner's painted band between the second and the third face
	b.tag = CIV
	b.box(Vector3(0, 0.1455, 0), Vector3(0.05, 0.012, 0.05), Color.WHITE)
	b.tag = PLAIN
	# a bird on top with its wings spread, red feather tips
	b.box(Vector3(0, 0.215, 0), Vector3(0.026, 0.032, 0.03), SOOT)
	b.box(Vector3(0, 0.242, 0.0), Vector3(0.022, 0.026, 0.026), SOOT)
	b.box(Vector3(0, 0.242, 0.021), Vector3(0.01, 0.01, 0.018), GOLD)
	for side in [-1.0, 1.0]:
		_at(b, Vector3(side * 0.04, 0.23, -0.002), 0.0, 0.0, side * 0.45)
		b.box(Vector3.ZERO, Vector3(0.062, 0.012, 0.022), SOOT)
		b.box(Vector3(side * 0.035, -0.005, 0.0), Vector3(0.022, 0.013, 0.02), OCHRE)
	_home(b)
	return b.commit()

# ------------------------------------------------------------------ marker (cairn)

static func _marker(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1041)
	b.tag = PLAIN
	# courses of flat stones, two to a course, each course smaller than the one below and turned a little more: a heap that tapers to a pale capstone
	var courses := [[0.0, 0.07, 0.048, 0.013], [0.0125, 0.056, 0.04, 0.012], [0.0235, 0.044, 0.032, 0.012], [0.0345, 0.034, 0.026, 0.011], [0.0445, 0.024, 0.019, 0.011]]
	for i in courses.size():
		var cr: Array = courses[i]
		var col := STONE if i % 2 == 0 else STONE_LIGHT
		for k in 2:
			var sx := 0.007 * (1.0 - 2.0 * k) * (4 - i) / 4.0
			_at(b, Vector3(sx, float(cr[0]) + float(cr[3]) * 0.5, 0.004 * (2 * k - 1) * (4 - i) / 4.0), 0.6 * i + 0.9 * k + 0.3)
			var sc := 1.0 - 0.14 * k
			b.box(Vector3.ZERO, Vector3(float(cr[1]) * sc, float(cr[3]), float(cr[2]) * sc), col.darkened(0.06 * k), col.lightened(0.12))
	_home(b)
	b.blob(Vector3(0.0, 0.061, 0.0), Vector3(0.013, 0.009, 0.012), 5, 2, PALE_STONE, 0.12, STONE_LIGHT)
	# a few fallen stones round the foot
	for i in 3:
		var a := TAU * i / 3.0 + 0.8
		b.blob(Vector3(cos(a) * 0.058, 0.005, sin(a) * 0.05), Vector3(0.011, 0.007, 0.01), 4, 2, STONE_DARK.lightened(0.1 * i), 0.15)
	# a stake leaning against it from behind, with a rag in the owner's colour
	b.pole(Vector3(-0.012, 0.03, -0.018), Vector3(-0.022, 0.17, -0.028), 0.0035, WOOD_LIGHT, 3)
	b.tag = CIV
	var top := Vector3(-0.022, 0.168, -0.028)
	b.quad(top, top + Vector3(0.05, -0.006, 0.012), top + Vector3(0.046, -0.026, 0.01), top + Vector3(0, -0.032, 0), Color.WHITE, top + Vector3(0, -0.02, -1.0))
	b.tag = PLAIN
	return b.commit()

# ------------------------------------------------------------------ shrine

static func _shrine(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1042)
	b.tag = PLAIN
	# three steps of dressed stone, a stair up the front
	b.box(Vector3(0, 0.007, 0), Vector3(0.23, 0.014, 0.19), STONE_DARK, STONE)
	b.box(Vector3(0, 0.019, -0.004), Vector3(0.18, 0.012, 0.15), STONE, STONE_LIGHT)
	b.box(Vector3(0, 0.031, -0.01), Vector3(0.13, 0.012, 0.11), STONE_LIGHT, PALE_STONE)
	for k in 3:
		b.box(Vector3(0, 0.003 + 0.0085 * k, 0.098 - 0.011 * k + 0.0), Vector3(0.05 + 0.006 * (2 - k), 0.0085, 0.014), PALE_STONE.darkened(0.04 * k))
	# the cell: grey stone walls round a dark opening, a lintel over it, and a lamp burning inside
	var cy := 0.037
	b.box(Vector3(0, cy + 0.032, -0.014), Vector3(0.1, 0.064, 0.08), STONE, STONE_LIGHT)
	b.box(Vector3(0, cy + 0.028, 0.0275), Vector3(0.036, 0.048, 0.006), DARK)
	b.box(Vector3(0, cy + 0.056, 0.03), Vector3(0.054, 0.008, 0.012), STONE_DARK)
	for sx in [-0.024, 0.024]:
		b.box(Vector3(sx, cy + 0.026, 0.03), Vector3(0.008, 0.052, 0.012), STONE_LIGHT)
	b.tag = GLOW
	b.box(Vector3(0, cy + 0.01, 0.019), Vector3(0.009, 0.009, 0.009), EMBER_HI)
	b.tag = PLAIN
	# a corbelled roof of stacked slabs, each overhanging the last, with an upright finial stone
	b.box(Vector3(0, cy + 0.068, -0.014), Vector3(0.124, 0.01, 0.1), STONE_DARK, STONE)
	b.box(Vector3(0, cy + 0.078, -0.014), Vector3(0.1, 0.01, 0.08), STONE, STONE_LIGHT)
	b.box(Vector3(0, cy + 0.088, -0.014), Vector3(0.07, 0.01, 0.056), STONE_LIGHT, PALE_STONE)
	b.box(Vector3(0, cy + 0.098, -0.014), Vector3(0.04, 0.01, 0.034), STONE, STONE_LIGHT)
	b.prism(Vector3(0, cy + 0.103, -0.014), 0.011, 0.0, 0.028, 4, PALE_STONE, PALE_STONE, PI / 4.0)
	# moss on the north side of the roof and plinth
	b.blob(Vector3(0.03, cy + 0.07, -0.05), Vector3(0.02, 0.005, 0.012), 5, 2, MOSS, 0.2)
	b.blob(Vector3(-0.07, 0.015, -0.02), Vector3(0.012, 0.005, 0.02), 5, 2, MOSS, 0.2)
	# two standing stones before the stair with a cord of prayer ribbons strung between them
	for sx in [-0.085, 0.085]:
		_at(b, Vector3(sx, 0.0, 0.1), 0.0, 0.0, -sx * 0.5)
		b.box(Vector3(0, 0.034, 0), Vector3(0.018, 0.068, 0.014), STONE_LIGHT, PALE_STONE)
		b.box(Vector3(0, 0.032, 0.0075), Vector3(0.008, 0.012, 0.002), DARK)
		_home(b)
	b.pole(Vector3(-0.0885, 0.066, 0.1), Vector3(0.0885, 0.066, 0.1), 0.0013, ROPE, 3)
	b.tag = CIV
	for k in 5:
		var x := -0.066 + 0.033 * k
		var y := 0.066 - 0.006 * sin(PI * (k + 0.5) / 5.0)
		b.quad(Vector3(x - 0.007, y, 0.1), Vector3(x + 0.007, y, 0.1), Vector3(x + 0.006, y - 0.026, 0.1), Vector3(x - 0.006, y - 0.026, 0.1), Color.WHITE, Vector3(x, y, -1.0))
	b.tag = PLAIN
	# offerings: small piles of votive stones either side of the stair
	for sx in [-1.0, 1.0]:
		for k in 3:
			b.blob(Vector3(sx * (0.105 + 0.01 * (k % 2)), 0.008 + 0.007 * (k / 2), 0.055 - 0.011 * k), Vector3(0.009, 0.006, 0.008), 4, 2, STONE_LIGHT if k % 2 == 0 else STONE, 0.15)
	return b.commit()

# ------------------------------------------------------------------ signal post

static func _signal_post(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1043)
	b.tag = PLAIN
	var top := 0.36
	# the mast, set in a footing of stones, and three splayed legs lashed to it for a third of its height
	for i in 5:
		var a := TAU * i / 5.0
		b.blob(Vector3(cos(a) * 0.03, 0.006, sin(a) * 0.03), Vector3(0.014, 0.009, 0.013), 5, 2, STONE if i % 2 == 0 else STONE_DARK, 0.15)
	b.prism(Vector3.ZERO, 0.017, 0.011, top, 5, WOOD, WOOD_LIGHT, 0.4)
	for i in 3:
		var a := TAU * i / 3.0 + 0.5
		b.pole(Vector3(cos(a) * 0.075, 0.0, sin(a) * 0.075), Vector3(cos(a) * 0.012, 0.19, sin(a) * 0.012), 0.0045, BARK.lightened(0.08), 4)
		# a cross-tie between each leg and the mast, halfway up
		b.pole(Vector3(cos(a) * 0.047, 0.08, sin(a) * 0.047), Vector3(cos(a + TAU / 3.0) * 0.047, 0.08, sin(a + TAU / 3.0) * 0.047), 0.0028, WOOD, 3)
	# a platform near the top with a low rail, and the iron brazier set on it
	b.prism(Vector3(0, top - 0.04, 0), 0.056, 0.05, 0.008, 8, WOOD_LIGHT, WOOD_LIGHT.lightened(0.1), PI / 8.0)
	for i in 8:
		var a := TAU * i / 8.0 + PI / 8.0
		b.pole(Vector3(cos(a) * 0.052, top - 0.032, sin(a) * 0.052), Vector3(cos(a) * 0.052, top - 0.01, sin(a) * 0.052), 0.0024, WOOD, 3)
	b.prism(Vector3(0, top - 0.032, 0), 0.02, 0.034, 0.026, 7, IRON, IRON.lightened(0.12), 0.2)
	b.prism(Vector3(0, top - 0.006, 0), 0.036, 0.036, 0.004, 7, IRON.lightened(0.1), IRON.lightened(0.1), 0.2)
	b.tag = GLOW
	b.prism(Vector3(0, top - 0.002, 0), 0.026, 0.0, 0.058, 5, EMBER, EMBER, 0.0, 0.2)
	b.prism(Vector3(0.006, top - 0.002, 0.004), 0.016, 0.0, 0.044, 4, EMBER_HI, EMBER_HI, 0.5, 0.2)
	b.prism(Vector3(-0.012, top - 0.002, -0.006), 0.012, 0.0, 0.034, 4, Color("#ff9a20"), Color("#ff9a20"), 0.2, 0.2)
	b.tag = PLAIN
	# a hide drum slung on the front of the mast at man height, with its two sticks hanging beside it
	_at(b, Vector3(0, 0.15, 0.04), 0.0, PI * 0.5)
	b.prism(Vector3(0, -0.012, 0), 0.026, 0.026, 0.024, 8, WOOD_LIGHT, Color("#e2cb9c"), 0.0)
	b.prism(Vector3(0, -0.003, 0), 0.0275, 0.0275, 0.003, 8, ROPE, ROPE)
	_home(b)
	b.pole(Vector3(-0.017, 0.165, 0.016), Vector3(-0.026, 0.15, 0.056), 0.0028, ROPE, 3)
	b.pole(Vector3(0.017, 0.165, 0.016), Vector3(0.026, 0.15, 0.056), 0.0028, ROPE, 3)
	for sx in [-0.01, 0.01]:
		b.pole(Vector3(sx, 0.128, 0.05), Vector3(sx * 1.6, 0.108, 0.058), 0.0022, WOOD, 3)
	# the owner's pennant streaming from the mast head
	b.tag = CIV
	var head := Vector3(0, top - 0.004, 0)
	b.pole(head, head + Vector3(0, 0.052, 0), 0.0024, WOOD)
	b.quad(head + Vector3(0, 0.05, 0), head + Vector3(0.056, 0.042, 0.01), head + Vector3(0.05, 0.026, 0.008), head + Vector3(0, 0.03, 0), Color.WHITE, head + Vector3(0, 0.03, -1.0))
	b.tag = PLAIN
	# kindling and a stack of fuel logs at the foot
	for k in 3:
		b.pole(Vector3(0.05, 0.004 + 0.0075 * k, -0.04 + 0.006 * (k % 2)), Vector3(0.056, 0.004 + 0.0075 * k, 0.03 + 0.004 * (k % 2)), 0.0045, WOOD if k % 2 == 0 else WOOD_LIGHT, 3)
	return b.commit()

# ------------------------------------------------------------------ potter's workshop

static func _workshop(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1044)
	b.tag = PLAIN
	var hx := 0.1
	# a trodden earth yard, a clay-plastered back wall and side walls under the roof, timber posts at the corners and a beam along the open front
	b.box(Vector3(0, 0.004, 0.01), Vector3(hx * 2.0 + 0.03, 0.008, 0.2), EARTH, EARTH.lightened(0.06))
	b.box(Vector3(0, 0.046, -0.07), Vector3(hx * 2.0 - 0.01, 0.076, 0.018), CLAY_WALL, CLAY_WALL.lightened(0.06))
	for sx in [-1.0, 1.0]:
		b.box(Vector3(sx * (hx - 0.012), 0.046, -0.025), Vector3(0.018, 0.076, 0.09), CLAY_WALL.darkened(0.06), CLAY_WALL)
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(sx * (hx - 0.004), 0.0, -0.068), Vector3(sx * (hx - 0.004), 0.094, -0.068), 0.0055, WOOD, 4)
		b.pole(Vector3(sx * (hx - 0.004), 0.0, 0.02), Vector3(sx * (hx - 0.004), 0.08, 0.02), 0.0055, WOOD, 4)
	b.pole(Vector3(-hx, 0.08, 0.02), Vector3(hx, 0.08, 0.02), 0.0045, WOOD_LIGHT, 4)
	# timber framing showing on the back wall: a sill, a rail and two studs
	b.box(Vector3(0, 0.014, -0.0605), Vector3(hx * 2.0 - 0.02, 0.006, 0.003), WOOD)
	b.box(Vector3(0, 0.074, -0.0605), Vector3(hx * 2.0 - 0.02, 0.006, 0.003), WOOD)
	for sx in [-0.04, 0.04]:
		b.box(Vector3(sx, 0.044, -0.0605), Vector3(0.006, 0.06, 0.003), WOOD)
	# a lean-to thatch roof over the back half, high at the back and low at the front, with courses of darker thatch across it
	var inner := Vector3(0, 0.04, 0)
	var rb := Vector3(0, 0.1, -0.087)
	var rf := Vector3(0, 0.078, 0.036)
	b.quad(Vector3(-hx - 0.012, rb.y, rb.z), Vector3(hx + 0.012, rb.y, rb.z), Vector3(hx + 0.012, rf.y, rf.z), Vector3(-hx - 0.012, rf.y, rf.z), THATCH, inner)
	b.quad(Vector3(-hx - 0.012, rb.y - 0.012, rb.z), Vector3(hx + 0.012, rb.y - 0.012, rb.z), Vector3(hx + 0.012, rf.y - 0.012, rf.z), Vector3(-hx - 0.012, rf.y - 0.012, rf.z), THATCH_DARK, inner)
	for sx in [-1.0, 1.0]:
		b.quad(Vector3(sx * (hx + 0.012), rb.y, rb.z), Vector3(sx * (hx + 0.012), rb.y - 0.012, rb.z), Vector3(sx * (hx + 0.012), rf.y - 0.012, rf.z), Vector3(sx * (hx + 0.012), rf.y, rf.z), THATCH_DARK, inner)
	for k in 3:
		var t := 0.22 + 0.28 * k
		b.box(Vector3(0, lerpf(rb.y, rf.y, t) + 0.001, lerpf(rb.z, rf.z, t)), Vector3(hx * 2.0 + 0.022, 0.002, 0.006), THATCH_DARK)
	b.box(Vector3(0, rb.y + 0.003, rb.z + 0.004), Vector3(hx * 2.0 + 0.028, 0.008, 0.012), THATCH_DARK, THATCH)
	# a shelf of jars along the back wall, and one drying on the floor
	b.box(Vector3(0, 0.044, -0.05), Vector3(hx * 2.0 - 0.05, 0.005, 0.026), WOOD_LIGHT)
	for k in 4:
		_jar(b, Vector3(-0.06 + 0.04 * k, 0.0465, -0.05), 0.85 + 0.15 * (k % 3), CLAY if k % 2 == 0 else CLAY_DARK.lightened(0.1))
	_jar(b, Vector3(-0.07, 0.008, -0.025), 1.3, CLAY.lightened(0.05))
	# the potter's wheel under the roof: a post with a heavy flywheel low down and a small throwing disc above, a lump of clay on it, and a stool beside it
	b.prism(Vector3(0.03, 0.008, -0.015), 0.01, 0.008, 0.044, 5, WOOD, WOOD_LIGHT)
	b.prism(Vector3(0.03, 0.012, -0.015), 0.026, 0.026, 0.007, 8, STONE_DARK, STONE)
	b.prism(Vector3(0.03, 0.05, -0.015), 0.019, 0.019, 0.005, 8, CLAY_DARK, CLAY)
	b.blob(Vector3(0.03, 0.06, -0.015), Vector3(0.009, 0.01, 0.009), 5, 2, CLAY.lightened(0.15), 0.1)
	b.prism(Vector3(-0.005, 0.008, 0.012), 0.014, 0.012, 0.02, 5, WOOD_LIGHT, WOOD)
	# a board of shaped pots drying out in the yard in front
	b.box(Vector3(-0.045, 0.012, 0.078), Vector3(0.09, 0.005, 0.026), WOOD_LIGHT)
	for sx in [-0.082, -0.008]:
		b.box(Vector3(sx, 0.005, 0.078), Vector3(0.006, 0.01, 0.022), WOOD)
	for k in 3:
		_jar(b, Vector3(-0.07 + 0.026 * k, 0.0145, 0.078), 0.8 + 0.1 * k, CLAY.lightened(0.1 * (k % 2)))
	# the small kiln on the +X side: a clay dome with a fire mouth, a vent and a stack of firewood
	var kx := hx + 0.055
	var courses := [[0.0, 0.04, 0.036, 0.018], [0.018, 0.036, 0.029, 0.016], [0.034, 0.029, 0.019, 0.014], [0.048, 0.019, 0.01, 0.01]]
	for i in courses.size():
		var cr: Array = courses[i]
		var col := CLAY if i % 2 == 0 else CLAY_DARK
		b.prism(Vector3(kx, cr[0], 0.0), cr[1], cr[2], cr[3], 8, col, col.lightened(0.08), PI / 8.0, 0.03)
	b.prism(Vector3(kx, 0.057, 0.0), 0.009, 0.007, 0.016, 6, CLAY_DARK, DARK)
	b.box(Vector3(kx, 0.012, 0.038), Vector3(0.026, 0.024, 0.012), CLAY_DARK, CLAY)
	b.tag = GLOW
	b.quad(Vector3(kx - 0.007, 0.002, 0.0445), Vector3(kx + 0.007, 0.002, 0.0445), Vector3(kx + 0.005, 0.017, 0.0445), Vector3(kx - 0.005, 0.017, 0.0445), EMBER, Vector3(kx, 0.01, 0.0))
	b.tag = PLAIN
	for i in 3:
		b.pole(Vector3(kx + 0.044, 0.004 + 0.007 * i, -0.036 + 0.006 * i), Vector3(kx + 0.05, 0.004 + 0.007 * i, 0.0 + 0.006 * i), 0.0042, WOOD if i % 2 == 0 else WOOD_LIGHT, 3)
	return b.commit()
