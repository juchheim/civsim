## Faith & Fief (E5) figures: the monk (BEL-10), the gunner (WAR-12), the pilgrim (BEL-11) and the mounted knight (WAR-10), docs/MVP/32 §6.
##
## Not wired to the simulation yet (docs/MVP/32 §9); photographed in the Fief gallery. They follow the people kit in mesh_kit.gd (`_person`): the same
## proportions (an adult is ~0.06 tall, hand at (0.026, 0.008, 0.012)), the owner's colour as a mantle or surcoat (TAG_CIV) so the owner reads from above,
## facing +Z, and are placed like the other figures (the instance colour carries the civilization). Monks, gunners and pilgrims are real adults of the
## settlement; the knight is a soldier with a horse and steel arms and is drawn mounted, one mesh for horse and rider.
##
##   person_monk        a brown robe to the ground with a rope belt, a hood, a tonsure, a small scapular in the owner's colour, a hoe
##   person_monk_book   the same holding a book against the chest (scribing, teaching)
##   person_gunner      a tunic and soft cap, a powder horn at the hip and a linstock (a long pole with a glowing match) in the hand
##   person_pilgrim     a long travelling cloak, a wide hat with a scallop badge, a staff with a gourd and a pack on the back
##   person_knight      a mounted knight: a bay horse under a caparison in the owner's colour, a rider in steel with a great helm and a plume, a kite shield
##                      in the owner's colour and a couched lance with a pennon
extends RefCounted

const SKIN := Color("#b07a52")
const HAIR := Color("#2b201a")
const WOOD_LIGHT := Color("#8a6a48")
const WOOD := Color("#5e4631")
const ROBE := Color("#6a5238")
const ROBE_DARK := Color("#54402c")
const ROPE := Color("#c8b88a")
const BOOK := Color("#3a2a1e")
const GOLD := Color("#c9973c")
const TUNIC := Color("#8a6a48")
const LEG := Color("#4a3a2a")
const CAP := Color("#4a3a2a")
const HORN := Color("#d8c9a0")
const CLOAK := Color("#6a5d50")
const CLOAK_DARK := Color("#54493e")
const HAT := Color("#4a3a2a")
const STEEL := Color("#8d949c")
const STEEL_DARK := Color("#5f666e")
const GLOW := Color("#ff7a24")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE

static func build(kit) -> void:
	kit.meshes["person_monk"] = _monk(kit, false)
	kit.meshes["person_monk_book"] = _monk(kit, true)
	kit.meshes["person_gunner"] = _gunner(kit)
	kit.meshes["person_pilgrim"] = _pilgrim(kit)
	kit.meshes["person_knight"] = _knight(kit)

## The head, in the people kit's size and place, with hair of the given colour.
static func _head(b: M7MeshBuilder, y: float = 0.05, hair: Color = HAIR) -> void:
	b.tag = PLAIN
	b.blob(Vector3(0, y, 0), Vector3(0.0125, 0.013, 0.0125), 7, 4, SKIN, 0.0)
	b.blob(Vector3(0, y + 0.005, -0.003), Vector3(0.0128, 0.009, 0.012), 7, 3, hair, 0.0)

static func _monk(kit, book: bool) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1601 + (1 if book else 0))
	b.tag = PLAIN
	# the habit: a long robe from the shoulders to the ground, wider at the hem, belted with a rope
	b.prism(Vector3(0, 0.0, 0), 0.0235, 0.0145, 0.056, 8, ROBE, ROBE)
	b.prism(Vector3(0, 0.0, 0), 0.0245, 0.0235, 0.004, 8, ROBE_DARK, ROBE_DARK)
	b.prism(Vector3(0, 0.027, 0), 0.0172, 0.0172, 0.0035, 8, ROPE, ROPE)
	b.pole(Vector3(0.007, 0.028, 0.016), Vector3(0.011, 0.012, 0.018), 0.0014, ROPE)
	# the owner's colour as a narrow scapular over the shoulders (a monk is one of a community, not a soldier)
	b.tag = CIV
	# (it must stand a little proud of the robe, which is 0.0235 wide at the foot and 0.0145 at the shoulders)
	b.prism(Vector3(0, 0.034, 0), 0.0202, 0.0172, 0.02, 8, Color.WHITE, Color.WHITE)
	b.tag = PLAIN
	# the head: a tonsure (bare crown with a ring of hair) and the hood thrown back on the shoulders
	b.blob(Vector3(0, 0.05, 0), Vector3(0.0125, 0.013, 0.0125), 7, 4, SKIN, 0.0)
	b.prism(Vector3(0, 0.0545, 0), 0.0128, 0.0124, 0.004, 8, HAIR, HAIR)
	b.blob(Vector3(0, 0.0568, 0), Vector3(0.0082, 0.004, 0.0082), 6, 2, SKIN, 0.0)
	b.blob(Vector3(0, 0.042, -0.011), Vector3(0.0165, 0.0095, 0.007), 6, 3, ROBE_DARK, 0.0)
	# the right hand: a hoe for the garden, or a book held against the chest
	if book:
		b.pole(Vector3(0.0215, 0.036, 0.004), Vector3(0.014, 0.034, 0.016), 0.0042, ROBE)
		b.pole(Vector3(-0.0215, 0.036, 0.004), Vector3(-0.006, 0.032, 0.017), 0.0042, ROBE)
		b.box(Vector3(0.004, 0.034, 0.0185), Vector3(0.022, 0.016, 0.005), BOOK)
		b.box(Vector3(0.004, 0.034, 0.0215), Vector3(0.002, 0.014, 0.001), GOLD)
		b.box(Vector3(0.004, 0.0345, 0.0165), Vector3(0.02, 0.014, 0.0012), Color("#efe6d0"))
	else:
		b.pole(Vector3(-0.0215, 0.036, 0.004), Vector3(-0.026, 0.014, 0.01), 0.0042, ROBE)
		b.pole(Vector3(0.0215, 0.036, 0.004), Vector3(0.028, 0.016, 0.012), 0.0042, ROBE)
		b.blob(Vector3(0.0285, 0.014, 0.0125), Vector3(0.0052, 0.0052, 0.0052), 5, 2, SKIN)
		var hand := Vector3(0.028, 0.012, 0.012)
		b.pole(hand + Vector3(0, -0.03, -0.006), hand + Vector3(0.002, 0.055, 0.01), 0.0026, WOOD_LIGHT)
		b.box(hand + Vector3(0.002, -0.032, 0.0), Vector3(0.006, 0.006, 0.018), Color("#6b717a"))
	return b.commit()

static func _gunner(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1603)
	b.tag = PLAIN
	b.prism(Vector3(-0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, LEG, LEG)
	b.prism(Vector3(0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, LEG, LEG)
	for sx in [-0.0075, 0.0075]:
		b.prism(Vector3(sx, 0, 0), 0.0075, 0.0072, 0.01, 5, Color("#2f2418"), Color("#2f2418"))
	# a short tunic in dark powder-stained cloth with a leather belt, and the owner's colour across the shoulders
	b.prism(Vector3(0, 0.02, 0), 0.0185, 0.0155, 0.038, 7, Color("#5a4a3a"), Color("#5a4a3a"))
	b.prism(Vector3(0, 0.024, 0), 0.0188, 0.0188, 0.004, 7, Color("#2f2418"), Color("#2f2418"))
	b.tag = CIV
	b.prism(Vector3(0, 0.044, 0), 0.0205, 0.009, 0.014, 7, Color.WHITE, Color.WHITE)
	b.tag = PLAIN
	b.pole(Vector3(-0.02, 0.05, 0), Vector3(-0.024, 0.028, 0.006), 0.0045, Color("#5a4a3a"))
	b.pole(Vector3(0.02, 0.05, 0), Vector3(0.026, 0.03, 0.01), 0.0045, Color("#5a4a3a"))
	_head(b, 0.07, HAIR)
	# the soft cap with a brim turned up at the front
	b.blob(Vector3(0, 0.0775, -0.001), Vector3(0.0132, 0.009, 0.0132), 7, 3, CAP, 0.0)
	b.box(Vector3(0, 0.0705, 0.0125), Vector3(0.016, 0.004, 0.004), CAP)
	# the powder horn on a cord at the hip: a curved cone, pale
	b.pole(Vector3(-0.012, 0.03, 0.012), Vector3(-0.022, 0.02, 0.016), 0.0036, HORN)
	b.pole(Vector3(-0.022, 0.02, 0.016), Vector3(-0.026, 0.01, 0.014), 0.0022, HORN)
	b.pole(Vector3(-0.002, 0.052, 0.012), Vector3(-0.012, 0.03, 0.014), 0.0009, Color("#2f2418"))
	# the linstock: a long pole held upright, a forked head with a smouldering match that glows
	var hand := Vector3(0.026, 0.03, 0.012)
	b.pole(hand + Vector3(0, -0.032, 0), hand + Vector3(0.002, 0.07, 0.004), 0.0026, WOOD_LIGHT)
	b.pole(hand + Vector3(0.002, 0.07, 0.004), hand + Vector3(-0.004, 0.082, 0.004), 0.0016, WOOD)
	b.pole(hand + Vector3(0.002, 0.07, 0.004), hand + Vector3(0.008, 0.082, 0.004), 0.0016, WOOD)
	b.tag = EMISSIVE
	b.blob(hand + Vector3(0.0, 0.0845, 0.004), Vector3(0.0042, 0.0042, 0.0042), 5, 3, GLOW)
	b.tag = PLAIN
	return b.commit()

static func _pilgrim(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1604)
	b.tag = PLAIN
	# a long cloak to the shins, dark and travel-worn, hem darker, with the owner's colour as a small cape at the shoulders
	b.prism(Vector3(0, 0.006, 0), 0.0215, 0.0155, 0.05, 8, CLOAK, CLOAK)
	b.prism(Vector3(0, 0.006, 0), 0.022, 0.0215, 0.005, 8, CLOAK_DARK, CLOAK_DARK)
	b.prism(Vector3(-0.0075, 0, 0), 0.0055, 0.0055, 0.012, 5, Color("#2f2418"), Color("#2f2418"))
	b.prism(Vector3(0.0075, 0, 0), 0.0055, 0.0055, 0.012, 5, Color("#2f2418"), Color("#2f2418"))
	b.tag = CIV
	b.prism(Vector3(0, 0.033, 0), 0.0195, 0.0095, 0.018, 8, Color.WHITE, Color.WHITE)
	b.tag = PLAIN
	_head(b, 0.052, Color("#3a2a20"))
	# a wide-brimmed hat and a scallop badge on its front, gold: five triangles fanning from the hat
	b.prism(Vector3(0, 0.06, 0), 0.026, 0.026, 0.0022, 10, HAT, HAT)
	b.prism(Vector3(0, 0.0618, 0), 0.0125, 0.0105, 0.01, 8, HAT, HAT.lightened(0.04))
	for k in 5:
		var a := (k - 2) * 0.42
		b.tri(Vector3(0, 0.064, 0.0235), Vector3(sin(a - 0.2) * 0.011, 0.064 + cos(a - 0.2) * 0.01, 0.0245), Vector3(sin(a + 0.2) * 0.011, 0.064 + cos(a + 0.2) * 0.01, 0.0245),
			GOLD if k % 2 == 0 else GOLD.lightened(0.15), Vector3(0, 0.06, 0.0))
	# a pack on the back (a bundle with a roll on top) and the staff in the right hand with a gourd hung from it
	b.box(Vector3(0, 0.032, -0.026), Vector3(0.026, 0.028, 0.014), Color("#a5814f"))
	b.prism(Vector3(-0.013, 0.0465, -0.026), 0.006, 0.006, 0.026, 6, Color("#7a6a4a"), Color("#7a6a4a"))
	b.pole(Vector3(0.0215, 0.04, 0.004), Vector3(0.026, 0.02, 0.01), 0.0044, CLOAK)
	b.pole(Vector3(-0.0215, 0.04, 0.004), Vector3(-0.026, 0.02, 0.008), 0.0044, CLOAK)
	var hand := Vector3(0.0275, 0.02, 0.012)
	b.pole(hand + Vector3(0, -0.02, -0.004), hand + Vector3(0.002, 0.07, 0.006), 0.0026, WOOD_LIGHT)
	b.blob(hand + Vector3(0.006, 0.058, 0.006), Vector3(0.0055, 0.0075, 0.0055), 6, 3, Color("#c9a050"), 0.0, Color("#a58038"))
	return b.commit()

static func _knight(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(1605)
	b.tag = PLAIN
	# the destrier: the people kit's horse in bay, under a caparison in the owner's colour draped over the barrel and quarters
	M7LooksKit._horse(b, Vector3.ZERO, M7LooksKit.HORSE_BAY, M7LooksKit.HORSE_DARK, 0.0)
	b.tag = CIV
	b.blob(Vector3(0, 0.0475, -0.004), Vector3(0.0138, 0.0185, 0.0235), 7, 4, Color.WHITE, 0.0)
	b.tag = PLAIN
	# a stripe of steel on the horse's brow (the chanfron) and a saddle
	b.box(Vector3(0, 0.0955, 0.052), Vector3(0.004, 0.012, 0.008), STEEL)
	b.box(Vector3(0, 0.0655, -0.003), Vector3(0.019, 0.005, 0.02), M7LooksKit.HORSE_DARK)
	# the rider, seated: legs hanging to the stirrups, a steel body under a surcoat in the owner's colour, arms to shield and lance
	for sx in [-1.0, 1.0]:
		b.pole(Vector3(sx * 0.011, 0.07, -0.003), Vector3(sx * 0.0175, 0.046, 0.004), 0.0048, STEEL_DARK)
		b.box(Vector3(sx * 0.0175, 0.044, 0.006), Vector3(0.004, 0.002, 0.007), STEEL)
	b.prism(Vector3(0, 0.068, -0.003), 0.0158, 0.0125, 0.034, 7, STEEL, STEEL_DARK)
	b.tag = CIV
	b.prism(Vector3(0, 0.068, -0.003), 0.0168, 0.0148, 0.028, 7, Color.WHITE, Color.WHITE)
	b.tag = PLAIN
	b.prism(Vector3(0, 0.0705, -0.003), 0.017, 0.017, 0.003, 7, Color("#2f2418"), Color("#2f2418"))
	# the great helm: a steel pot with a slit visor and a plume in the owner's colour
	b.prism(Vector3(0, 0.098, -0.003), 0.0118, 0.0105, 0.017, 8, STEEL, STEEL_DARK)
	b.blob(Vector3(0, 0.1145, -0.003), Vector3(0.0108, 0.006, 0.0108), 7, 3, STEEL, 0.0)
	b.box(Vector3(0, 0.1055, 0.0075), Vector3(0.012, 0.0022, 0.002), Color("#2a2119"))
	b.tag = CIV
	b.blob(Vector3(0, 0.12, -0.007), Vector3(0.004, 0.009, 0.01), 5, 3, Color.WHITE, 0.0)
	b.tag = PLAIN
	# the shield on the left arm: a kite in the owner's colour with a steel rim and boss
	b.pole(Vector3(-0.0165, 0.092, -0.003), Vector3(-0.022, 0.082, 0.012), 0.0042, STEEL_DARK)
	b.tag = CIV
	b.quad(Vector3(-0.0235, 0.098, 0.006), Vector3(-0.0235, 0.098, 0.022), Vector3(-0.0235, 0.082, 0.022), Vector3(-0.0235, 0.082, 0.006), Color.WHITE, Vector3(0.1, 0.09, 0.014))
	b.tri(Vector3(-0.0235, 0.082, 0.006), Vector3(-0.0235, 0.082, 0.022), Vector3(-0.0235, 0.068, 0.014), Color.WHITE, Vector3(0.1, 0.082, 0.014))
	b.tag = PLAIN
	b.box(Vector3(-0.0238, 0.088, 0.014), Vector3(0.0015, 0.007, 0.007), STEEL)
	# the lance, couched under the right arm and pointing ahead and a little up, with a pennon in the owner's colour behind the steel head
	var hand := Vector3(0.0185, 0.088, 0.01)
	b.pole(hand + Vector3(0.0, -0.01, -0.05), hand + Vector3(0.006, 0.026, 0.13), 0.0026, M7LooksKit.HORSE_DARK.lightened(0.18))
	var tip := hand + Vector3(0.006, 0.026, 0.13)
	b.prism(tip, 0.0036, 0.0, 0.012, 4, STEEL, STEEL, 0.0)
	b.tag = CIV
	var pen := tip + Vector3(0, 0.0, -0.012)
	b.quad(pen, pen + Vector3(0.0, -0.0, -0.034), pen + Vector3(0.0, -0.014, -0.032), pen + Vector3(0.0, -0.01, 0.0), Color.WHITE, pen + Vector3(1.0, 0, 0))
	b.quad(pen, pen + Vector3(0.0, -0.0, -0.034), pen + Vector3(0.0, -0.014, -0.032), pen + Vector3(0.0, -0.01, 0.0), Color.WHITE, pen + Vector3(-1.0, 0, 0))
	b.tag = PLAIN
	return b.commit()
