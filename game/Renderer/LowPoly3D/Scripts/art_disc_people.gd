## Discovery (E6) figures: the engineer (NRG-06), the coal miner (MIN-09), the navigator (WAY-10), the botanist (FOR-09), the factory operative (MEC-10),
## the bargeman (TRN-11) and the coachman with a turnpike coach (TRN-10), docs/MVP/39 §6.
##
## Not wired to the simulation yet (docs/MVP/39 §9: `engineerStaff`, `botanistStaff` and the work states say who is at which building); photographed in the
## Discovery gallery (LowPoly3DView.show_discovery_gallery). They follow the people kit in mesh_kit.gd (`_person`) and art_fief_people.gd: the same proportions
## (a villager is ~0.097 tall, legs 0.036, the torso lifted by 0.014 over them, a hand at about (0.026, 0.03, 0.012)), the owner's colour across the
## shoulders (TAG_CIV) so the owner reads from above, facing +Z, placed like the other figures (the instance colour carries the civilization). Each carries the
## thing that says its job at overview zoom, not just in a close-up: an oil can, a lamp, a spyglass, a straw hat, a bobbin, a pole, a whip.
##
##   person_engineer   a flat cap, a grey shirt with the sleeves rolled, a long brown leather apron, a smudge on the cheek, an oil can in one hand and a spanner
##                     in the other: the two who tend the atmospheric engine
##   person_miner      soot-black clothes and face, a cap with a lit lamp on the front, a pick over the shoulder and a sack of coal on the back
##   person_navigator  a long dark-blue coat with brass buttons, a white cravat, a bicorne hat, a brass spyglass at the eye and the chronometer box under the arm
##   person_botanist   a long green smock, a wide straw hat, a tin collecting case on a strap, a trowel in one hand and a potted seedling in the other
##   person_operative  a cap, a shirt with rolled sleeves, a blue-grey work apron, a bobbin of thread in one hand: the factory's twelve
##   person_bargeman   a broad-brimmed hat, a brown jerkin and rolled trousers, a long barge pole in the hands and a coil of tow rope on the shoulder
##   person_coachman   a tall hat, a long greatcoat with two capes, a scarf and a long whip held up
##   coach             a turnpike stage coach, +Z forward: a boxy body in the owner's colour on four spoked wheels with a roof of luggage, a coach box with the
##                     coachman and a whip, a passenger's head at each window, lamps, and a pair of horses in harness
extends RefCounted

const SKIN := Color("#b07a52")
const SOOT_SKIN := Color("#3a332e")
const HAIR := Color("#2b201a")
const WOOD := Color("#5e4631")
const WOOD_LIGHT := Color("#8a6a48")
const LEATHER := Color("#6b4a2e")
const LEATHER_DARK := Color("#4a3320")
const STEEL := Color("#8d949c")
const STEEL_DARK := Color("#5f666e")
const BRASS := Color("#c9973c")
const BRASS_LIGHT := Color("#e0b660")
const COAL := Color("#26262b")
const COAL_LIGHT := Color("#3d3d44")
const SOOT_COAT := Color("#34322f")
const LEG := Color("#4a3a2a")
const BOOT := Color("#2f2418")
const SHIRT := Color("#8a8f98")
const SHIRT_LIGHT := Color("#c9ccd2")
const APRON_BLUE := Color("#5a6c7c")
const NAVY := Color("#2a3a5a")
const NAVY_DARK := Color("#1f2c46")
const CRAVAT := Color("#efe9da")
const SMOCK := Color("#6a8a4a")
const SMOCK_DARK := Color("#506c36")
const STRAW := Color("#d8c27a")
const STRAW_DARK := Color("#b8a45a")
const TIN := Color("#b7bcc2")
const POT := Color("#b4673a")
const LEAF := Color("#4f8a38")
const JERKIN := Color("#7a5a3c")
const ROPE := Color("#c8b88a")
const BLACK := Color("#1d1a18")
const GREATCOAT := Color("#4a4a52")
const LAMP := Color("#ffd27a")
const GLASS := Color("#39485a")
const COACH_BODY := Color("#2f3a46")
const COACH_TRIM := Color("#c9973c")
const WHEEL := Color("#8a6a48")
const HORSE := Color("#7a4e2e")
const HORSE_DARK := Color("#54351f")
const HORSE_PALE := Color("#b89a72")
const PLAIN := M7MeshBuilder.TAG_PLAIN
const CIV := M7MeshBuilder.TAG_CIV
const EMISSIVE := M7MeshBuilder.TAG_EMISSIVE

static func build(kit) -> void:
	kit.meshes["person_engineer"] = _engineer(kit)
	kit.meshes["person_miner"] = _miner(kit)
	kit.meshes["person_navigator"] = _navigator(kit)
	kit.meshes["person_botanist"] = _botanist(kit)
	kit.meshes["person_operative"] = _operative(kit)
	kit.meshes["person_bargeman"] = _bargeman(kit)
	kit.meshes["person_coachman"] = _coachman(kit)
	kit.meshes["coach"] = _coach(kit)

# ------------------------------------------------------------------ shared pieces

## The legs and boots, then the frame lifted by 0.014 as the gunner's: everything the caller draws after this stands on the legs.
static func _legs(b: M7MeshBuilder, legc: Color = LEG, boot: Color = BOOT) -> void:
	b.tag = PLAIN
	b.prism(Vector3(-0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, legc, legc)
	b.prism(Vector3(0.0075, 0, 0), 0.0065, 0.007, 0.036, 5, legc, legc)
	for sx in [-0.0075, 0.0075]:
		var x: float = sx
		b.prism(Vector3(x, 0, 0), 0.0075, 0.0072, 0.01, 5, boot, boot)
	b.xf = b.xf * Transform3D(Basis.IDENTITY, Vector3(0, 0.014, 0))

## The torso (a tunic of `coat` colour to the hip with a belt), the owner's colour across the shoulders, and two arms in `sleeve`.
static func _torso(b: M7MeshBuilder, coat: Color, sleeve: Color, belt: Color = BOOT) -> void:
	b.tag = PLAIN
	b.prism(Vector3(0, 0.02, 0), 0.0185, 0.0155, 0.038, 7, coat, coat)
	b.prism(Vector3(0, 0.024, 0), 0.0188, 0.0188, 0.004, 7, belt, belt)
	b.tag = CIV
	b.prism(Vector3(0, 0.044, 0), 0.0205, 0.009, 0.014, 7, Color.WHITE, Color.WHITE)
	b.tag = PLAIN
	b.pole(Vector3(-0.02, 0.05, 0), Vector3(-0.024, 0.028, 0.006), 0.0045, sleeve)
	b.pole(Vector3(0.02, 0.05, 0), Vector3(0.026, 0.03, 0.01), 0.0045, sleeve)

static func _head(b: M7MeshBuilder, skin: Color = SKIN, hair: Color = HAIR) -> void:
	b.tag = PLAIN
	b.blob(Vector3(0, 0.07, 0), Vector3(0.0125, 0.013, 0.0125), 7, 4, skin, 0.0)
	b.blob(Vector3(0, 0.075, -0.003), Vector3(0.0128, 0.009, 0.012), 7, 3, hair, 0.0)

static func _hand(b: M7MeshBuilder, p: Vector3, skin: Color = SKIN) -> void:
	b.blob(p, Vector3(0.0048, 0.0048, 0.0048), 5, 2, skin, 0.0)

# ------------------------------------------------------------------ engineer, miner, navigator, botanist, operative

static func _engineer(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2400)
	_legs(b)
	_torso(b, SHIRT, SHIRT_LIGHT)
	# the leather apron: a long panel over the front from the chest to the knee, with a bib and a neck strap
	b.box(Vector3(0, 0.026, 0.0165), Vector3(0.026, 0.044, 0.004), LEATHER, LEATHER.lightened(0.05))
	b.box(Vector3(0, 0.0515, 0.0155), Vector3(0.016, 0.014, 0.004), LEATHER_DARK)
	b.pole(Vector3(-0.007, 0.056, 0.014), Vector3(-0.003, 0.064, 0.0), 0.0012, LEATHER_DARK)
	b.pole(Vector3(0.007, 0.056, 0.014), Vector3(0.003, 0.064, 0.0), 0.0012, LEATHER_DARK)
	# the forearms bare where the sleeves are rolled
	_hand(b, Vector3(-0.0245, 0.028, 0.0065))
	_hand(b, Vector3(0.0265, 0.03, 0.0115))
	_head(b)
	# a flat cap with a short peak, and a smudge of soot on the cheek
	b.blob(Vector3(0, 0.0775, -0.001), Vector3(0.0138, 0.007, 0.0138), 7, 3, Color("#3a342e"), 0.0)
	b.box(Vector3(0, 0.0725, 0.0128), Vector3(0.016, 0.003, 0.005), Color("#3a342e"))
	b.blob(Vector3(0.0075, 0.066, 0.0115), Vector3(0.003, 0.0026, 0.002), 4, 2, SOOT_SKIN, 0.0)
	# an oil can in the right hand: a squat tin body with a long thin spout
	var hand := Vector3(0.0265, 0.03, 0.0115)
	b.prism(hand + Vector3(0, -0.002, 0.004), 0.0045, 0.0045, 0.01, 6, TIN, TIN.lightened(0.1))
	b.pole(hand + Vector3(0, 0.006, 0.004), hand + Vector3(0.002, 0.012, 0.016), 0.0012, TIN, 3)
	# a spanner in the left hand: a long shaft with an open jaw at the end
	var lh := Vector3(-0.0245, 0.028, 0.0065)
	b.pole(lh, lh + Vector3(-0.004, -0.02, 0.014), 0.0016, STEEL, 4)
	b.box(lh + Vector3(-0.005, -0.021, 0.0155), Vector3(0.008, 0.003, 0.006), STEEL_DARK)
	return b.commit()

static func _miner(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2401)
	_legs(b, SOOT_COAT, Color("#1f1c1a"))
	_torso(b, SOOT_COAT, SOOT_COAT, Color("#1f1c1a"))
	_hand(b, Vector3(-0.0245, 0.028, 0.0065), SOOT_SKIN)
	_hand(b, Vector3(0.0265, 0.03, 0.0115), SOOT_SKIN)
	_head(b, SOOT_SKIN, Color("#1f1c1a"))
	# a tight dark cap with a lamp on the front, lit
	b.blob(Vector3(0, 0.0775, -0.001), Vector3(0.0135, 0.0085, 0.0135), 7, 3, COAL, 0.0)
	b.box(Vector3(0, 0.0735, 0.0128), Vector3(0.012, 0.003, 0.004), COAL_LIGHT)
	b.box(Vector3(0, 0.0785, 0.0132), Vector3(0.0072, 0.0072, 0.005), STEEL_DARK)
	b.tag = EMISSIVE
	b.box(Vector3(0, 0.0785, 0.0162), Vector3(0.0048, 0.0048, 0.0012), LAMP)
	b.tag = PLAIN
	# the whites of the eyes, which is all that shows of the face
	b.box(Vector3(-0.0042, 0.0705, 0.0125), Vector3(0.0028, 0.0018, 0.0012), Color("#e6e0d2"))
	b.box(Vector3(0.0042, 0.0705, 0.0125), Vector3(0.0028, 0.0018, 0.0012), Color("#e6e0d2"))
	# a pick over the right shoulder: a long handle running up and back, an iron head crossing it at the top
	var hand := Vector3(0.0265, 0.03, 0.0115)
	b.pole(hand + Vector3(0, -0.012, 0.004), hand + Vector3(-0.012, 0.062, -0.01), 0.0026, WOOD_LIGHT)
	b.box(hand + Vector3(-0.012, 0.064, -0.01), Vector3(0.0036, 0.0036, 0.026), STEEL_DARK)
	# a sack of coal on the back
	b.blob(Vector3(0, 0.036, -0.0235), Vector3(0.0135, 0.0185, 0.0085), 6, 3, Color("#46403a"), 0.1, Color("#2b2723"))
	b.blob(Vector3(0.003, 0.054, -0.0235), Vector3(0.0045, 0.004, 0.004), 4, 2, COAL, 0.2)
	return b.commit()

static func _navigator(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2402)
	_legs(b, Color("#d8d2c0"), BOOT)     # pale breeches and dark boots
	# a long frock coat to the knee: wider at the hem, in dark navy, with a row of brass buttons and turned cuffs
	b.tag = PLAIN
	b.prism(Vector3(0, 0.004, 0), 0.0215, 0.0155, 0.054, 7, NAVY, NAVY)
	b.prism(Vector3(0, 0.003, 0), 0.0222, 0.0215, 0.004, 7, NAVY_DARK, NAVY_DARK)
	b.prism(Vector3(0, 0.024, 0), 0.0188, 0.0188, 0.003, 7, BOOT, BOOT)
	for k in 4:
		b.blob(Vector3(0, 0.012 + 0.012 * k, 0.0178 - 0.0003 * k), Vector3(0.0022, 0.0022, 0.0012), 4, 2, BRASS_LIGHT, 0.0)
	b.tag = CIV
	b.prism(Vector3(0, 0.044, 0), 0.0205, 0.009, 0.014, 7, Color.WHITE, Color.WHITE)
	b.tag = PLAIN
	b.pole(Vector3(-0.02, 0.05, 0), Vector3(-0.024, 0.028, 0.006), 0.0047, NAVY)
	b.pole(Vector3(0.02, 0.05, 0), Vector3(0.016, 0.062, 0.02), 0.0047, NAVY)
	_hand(b, Vector3(-0.0245, 0.028, 0.0065))
	_head(b, SKIN, Color("#6a5a48"))
	b.box(Vector3(0, 0.058, 0.012), Vector3(0.016, 0.005, 0.005), CRAVAT)
	# a bicorne hat worn athwart: a flat black crown turned up at both sides
	b.blob(Vector3(0, 0.0805, 0.0), Vector3(0.0125, 0.0055, 0.0125), 7, 3, BLACK, 0.0)
	b.box(Vector3(-0.0135, 0.0795, 0.0), Vector3(0.006, 0.0075, 0.021), BLACK)
	b.box(Vector3(0.0135, 0.0795, 0.0), Vector3(0.006, 0.0075, 0.021), BLACK)
	b.box(Vector3(0, 0.0745, 0.0), Vector3(0.02, 0.0025, 0.0225), BLACK)
	b.tag = CIV
	b.box(Vector3(0.0, 0.0845, 0.0112), Vector3(0.005, 0.0022, 0.0014), Color.WHITE)
	b.tag = PLAIN
	# the spyglass at the right eye: a brass tube with a wider end bell, drawn out to the full length
	var eye := Vector3(0.004, 0.071, 0.0135)
	var far := Vector3(0.029, 0.1, 0.03)
	b.pole(eye, far, 0.0027, BRASS, 6)
	b.pole(eye.lerp(far, 0.55), far, 0.0034, BRASS_LIGHT, 6)
	b.blob(far, Vector3(0.0038, 0.0038, 0.0038), 5, 2, GLASS, 0.0)
	# the chronometer in its box under the left arm
	b.box(Vector3(-0.0275, 0.03, 0.01), Vector3(0.012, 0.011, 0.014), LEATHER_DARK)
	b.blob(Vector3(-0.0215, 0.03, 0.01), Vector3(0.0016, 0.0036, 0.0036), 4, 2, BRASS_LIGHT, 0.0)
	return b.commit()

static func _botanist(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2403)
	b.tag = PLAIN
	# a long smock to the ground, wider at the hem, belted at the waist with a cord
	b.prism(Vector3(0, 0.0, 0), 0.0225, 0.0155, 0.088, 8, SMOCK, SMOCK)
	b.prism(Vector3(0, 0.0, 0), 0.0235, 0.0225, 0.004, 8, SMOCK_DARK, SMOCK_DARK)
	b.prism(Vector3(0, 0.04, 0), 0.0175, 0.0175, 0.003, 8, ROPE, ROPE)
	b.xf = b.xf * Transform3D(Basis.IDENTITY, Vector3(0, 0.034, 0))
	b.tag = CIV
	b.prism(Vector3(0, 0.034, 0), 0.0205, 0.0172, 0.016, 8, Color.WHITE, Color.WHITE)
	b.tag = PLAIN
	b.pole(Vector3(-0.0215, 0.036, 0.004), Vector3(-0.024, 0.016, 0.014), 0.0046, SMOCK_DARK)
	b.pole(Vector3(0.0215, 0.036, 0.004), Vector3(0.026, 0.016, 0.014), 0.0046, SMOCK_DARK)
	_hand(b, Vector3(-0.0245, 0.0135, 0.0145))
	_hand(b, Vector3(0.0265, 0.0135, 0.0145))
	b.blob(Vector3(0, 0.05, 0), Vector3(0.0125, 0.013, 0.0125), 7, 4, SKIN, 0.0)
	b.blob(Vector3(0, 0.054, -0.004), Vector3(0.0128, 0.0085, 0.0125), 7, 3, Color("#6a5a48"), 0.0)
	# a wide straw hat: a broad flat brim over a low rounded crown with a green band
	b.prism(Vector3(0, 0.0585, 0), 0.0275, 0.0265, 0.0025, 10, STRAW, STRAW_DARK)
	b.blob(Vector3(0, 0.0635, 0), Vector3(0.0138, 0.0085, 0.0138), 7, 3, STRAW, 0.0, STRAW_DARK)
	b.prism(Vector3(0, 0.0605, 0), 0.0142, 0.014, 0.003, 8, SMOCK_DARK, SMOCK_DARK)
	# the tin collecting case (a vasculum) on a strap: a flat cylinder lying at the hip
	b.pole(Vector3(-0.008, 0.062, 0.014), Vector3(0.016, 0.026, 0.016), 0.0012, LEATHER_DARK, 3)
	b.prism(Vector3(0.0185, 0.018, 0.016), 0.0068, 0.0068, 0.026, 8, TIN, TIN.lightened(0.1), 0.0, 0.0, true)
	# a trowel in the right hand, the potted seedling in the left
	var rh := Vector3(0.0265, 0.0135, 0.0145)
	b.pole(rh, rh + Vector3(0.002, -0.012, 0.012), 0.0016, WOOD_LIGHT, 4)
	b.box(rh + Vector3(0.002, -0.014, 0.0145), Vector3(0.006, 0.002, 0.009), STEEL)
	var lh := Vector3(-0.0245, 0.0135, 0.0145)
	b.prism(lh + Vector3(0, -0.002, 0.003), 0.0062, 0.0045, 0.01, 7, POT, POT.darkened(0.2))
	b.blob(lh + Vector3(0, 0.0105, 0.003), Vector3(0.0068, 0.0058, 0.0068), 5, 2, LEAF, 0.1)
	return b.commit()

static func _operative(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2404)
	_legs(b, Color("#4a4540"), Color("#2f2418"))
	_torso(b, SHIRT_LIGHT, SHIRT_LIGHT, Color("#4a4540"))
	# a blue-grey work apron from the chest to the knee, with a pocket
	b.box(Vector3(0, 0.026, 0.0165), Vector3(0.028, 0.042, 0.004), APRON_BLUE, APRON_BLUE.lightened(0.06))
	b.box(Vector3(0.004, 0.034, 0.0192), Vector3(0.011, 0.009, 0.0015), APRON_BLUE.darkened(0.18))
	_hand(b, Vector3(-0.0245, 0.028, 0.0065))
	_hand(b, Vector3(0.0265, 0.03, 0.0115))
	_head(b, SKIN, Color("#4a3a2a"))
	# a plain flat cap
	b.blob(Vector3(0, 0.0775, -0.001), Vector3(0.0138, 0.0072, 0.0138), 7, 3, Color("#5a5048"), 0.0)
	b.box(Vector3(0, 0.0725, 0.0128), Vector3(0.016, 0.003, 0.005), Color("#5a5048"))
	# a bobbin of thread in the right hand: a spool with a flange at each end and the thread wound between
	var hand := Vector3(0.0265, 0.03, 0.0115)
	b.prism(hand + Vector3(0, 0.0, 0.006), 0.0068, 0.0068, 0.002, 8, WOOD_LIGHT, WOOD_LIGHT)
	b.prism(hand + Vector3(0, 0.002, 0.006), 0.0045, 0.0045, 0.012, 8, Color("#e8dcc0"), Color("#e8dcc0"))
	b.prism(hand + Vector3(0, 0.014, 0.006), 0.0068, 0.0068, 0.002, 8, WOOD_LIGHT, WOOD_LIGHT)
	return b.commit()

# ------------------------------------------------------------------ bargeman and coachman

static func _bargeman(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2405)
	_legs(b, Color("#5a4a3a"), Color("#2f2418"))
	_torso(b, JERKIN, Color("#d8d2c0"), Color("#2f2418"))
	_hand(b, Vector3(-0.0245, 0.028, 0.0065))
	_hand(b, Vector3(0.0265, 0.03, 0.0115))
	_head(b, SKIN, Color("#4a3a2a"))
	# a broad-brimmed felt hat
	b.prism(Vector3(0, 0.0735, 0), 0.0215, 0.021, 0.002, 9, Color("#4a4238"), Color("#4a4238"))
	b.prism(Vector3(0, 0.0745, 0), 0.0128, 0.0105, 0.01, 8, Color("#4a4238"), Color("#3a342e"))
	# a long barge pole, slanting forward from the hands and down to the ground, with an iron shoe and a hook near the top
	var hand := Vector3(0.0265, 0.03, 0.0115)
	b.pole(hand + Vector3(-0.004, 0.058, -0.018), hand + Vector3(0.006, -0.044, 0.05), 0.0026, WOOD_LIGHT, 4)
	b.box(hand + Vector3(0.0065, -0.045, 0.0515), Vector3(0.004, 0.006, 0.004), STEEL_DARK)
	b.pole(hand + Vector3(-0.004, 0.058, -0.018), hand + Vector3(-0.004, 0.062, -0.026), 0.0016, STEEL_DARK, 3)
	# a coil of tow rope over the left shoulder
	b.prism(Vector3(-0.013, 0.052, 0.0), 0.0092, 0.0092, 0.004, 8, ROPE, ROPE)
	b.prism(Vector3(-0.013, 0.0555, 0.0), 0.0082, 0.0082, 0.004, 8, ROPE.darkened(0.08), ROPE.darkened(0.08))
	return b.commit()

static func _coachman_into(b: M7MeshBuilder, seated: bool) -> void:
	b.tag = PLAIN
	if not seated:
		_legs(b, Color("#d8d2c0"), BOOT)
	# a long greatcoat to the knee, in the owner's grey, with two shoulder capes (the owner's colour over the top one)
	b.tag = PLAIN
	b.prism(Vector3(0, 0.002, 0), 0.0225, 0.0165, 0.056, 8, GREATCOAT, GREATCOAT)
	b.prism(Vector3(0, 0.003, 0), 0.0232, 0.0225, 0.004, 8, GREATCOAT.darkened(0.2), GREATCOAT.darkened(0.2))
	b.prism(Vector3(0, 0.034, 0), 0.0235, 0.0195, 0.013, 8, GREATCOAT.lightened(0.06), GREATCOAT.lightened(0.06))
	b.tag = CIV
	b.prism(Vector3(0, 0.044, 0), 0.0215, 0.01, 0.014, 8, Color.WHITE, Color.WHITE)
	b.tag = PLAIN
	b.pole(Vector3(-0.0205, 0.052, 0), Vector3(-0.026, 0.03, 0.01), 0.0048, GREATCOAT)
	b.pole(Vector3(0.0205, 0.052, 0), Vector3(0.026, 0.056, 0.018), 0.0048, GREATCOAT)
	_hand(b, Vector3(-0.0275, 0.0285, 0.0105))
	_hand(b, Vector3(0.0265, 0.0575, 0.0185))
	_head(b, SKIN, Color("#6a5a48"))
	b.prism(Vector3(0, 0.0575, 0.006), 0.0132, 0.0132, 0.005, 8, Color("#8a2f24"), Color("#8a2f24"))     # a red scarf
	# a tall black hat with a narrow brim
	b.prism(Vector3(0, 0.0745, 0), 0.0185, 0.0185, 0.0018, 9, BLACK, BLACK)
	b.prism(Vector3(0, 0.0762, 0), 0.0112, 0.0098, 0.018, 8, BLACK, Color("#2a2522"))
	# the whip held up: a short stock with a long thin lash trailing forward and down
	var hand := Vector3(0.0265, 0.0575, 0.0185)
	b.pole(hand + Vector3(0, -0.004, 0.0), hand + Vector3(0.006, 0.05, 0.022), 0.0022, WOOD_LIGHT, 4)
	b.pole(hand + Vector3(0.006, 0.05, 0.022), hand + Vector3(0.02, 0.058, 0.06), 0.0008, ROPE, 3)

static func _coachman(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2406)
	_coachman_into(b, false)
	return b.commit()

# ------------------------------------------------------------------ the coach

static func _horse(b: M7MeshBuilder, p: Vector3, col: Color) -> void:
	var dark := col.darkened(0.25)
	# a barrel body, a deep chest, four legs, a neck carrying a long head with ears, a mane and a tail
	b.blob(p + Vector3(0, 0.054, 0), Vector3(0.0155, 0.0175, 0.045), 7, 3, col, 0.04, col.darkened(0.12))
	b.blob(p + Vector3(0, 0.058, 0.032), Vector3(0.0165, 0.0185, 0.0155), 6, 3, col, 0.04)
	for sx in [-0.0085, 0.0085]:
		for sz in [-0.032, 0.036]:
			var x: float = sx
			var z: float = sz
			b.pole(p + Vector3(x, 0.046, z), p + Vector3(x, 0.0, z), 0.0042, dark, 4)
			b.box(p + Vector3(x, 0.002, z), Vector3(0.0075, 0.004, 0.0085), BLACK)
	b.pole(p + Vector3(0, 0.07, 0.042), p + Vector3(0, 0.1, 0.056), 0.0105, col, 5)
	b.blob(p + Vector3(0, 0.103, 0.0645), Vector3(0.0068, 0.0092, 0.0145), 6, 3, col, 0.0)
	b.box(p + Vector3(-0.0035, 0.113, 0.054), Vector3(0.0022, 0.0075, 0.0022), dark)
	b.box(p + Vector3(0.0035, 0.113, 0.054), Vector3(0.0022, 0.0075, 0.0022), dark)
	b.pole(p + Vector3(0, 0.108, 0.05), p + Vector3(0, 0.072, 0.038), 0.0045, dark, 4)
	b.pole(p + Vector3(0, 0.06, -0.044), p + Vector3(0, 0.032, -0.062), 0.0055, dark, 4)

static func _wheel(b: M7MeshBuilder, p: Vector3, r: float) -> void:
	# a spoked wheel with its axle along X (a ring in the YZ plane at x = p.x)
	for i in 12:
		var a0 := TAU * i / 12.0
		var a1 := TAU * (i + 1) / 12.0
		b.pole(p + Vector3(0, sin(a0), cos(a0)) * r, p + Vector3(0, sin(a1), cos(a1)) * r, 0.0034, WHEEL, 4)
	for i in 4:
		var a := PI * i / 4.0
		b.pole(p - Vector3(0, sin(a), cos(a)) * r, p + Vector3(0, sin(a), cos(a)) * r, 0.0018, WHEEL.darkened(0.15), 3)
	b.pole(p + Vector3(-0.007, 0, 0), p + Vector3(0.007, 0, 0), 0.0045, STEEL_DARK, 6)

static func _coach(kit) -> ArrayMesh:
	var b: M7MeshBuilder = kit._b(2407)
	b.tag = PLAIN
	# the chassis: two axles with the body slung between them, wheels larger at the rear
	b.pole(Vector3(-0.034, 0.034, -0.045), Vector3(0.034, 0.034, -0.045), 0.0035, STEEL_DARK, 4)
	b.pole(Vector3(-0.03, 0.028, 0.045), Vector3(0.03, 0.028, 0.045), 0.0035, STEEL_DARK, 4)
	for sx in [-0.04, 0.04]:
		var x: float = sx
		_wheel(b, Vector3(x, 0.034, -0.045), 0.034)
		_wheel(b, Vector3(x, 0.028, 0.045), 0.026)
	b.pole(Vector3(0, 0.034, -0.045), Vector3(0, 0.028, 0.045), 0.0035, WOOD, 4)
	# the body: a boxy cabin in the owner's colour over a dark undercarriage, gold trim at the waist
	b.box(Vector3(0, 0.047, -0.01), Vector3(0.07, 0.01, 0.1), COACH_BODY.darkened(0.2), WOOD)
	b.tag = CIV
	b.box(Vector3(0, 0.0715, -0.01), Vector3(0.07, 0.039, 0.1), Color.WHITE)
	b.tag = PLAIN
	b.box(Vector3(0, 0.0545, -0.01), Vector3(0.0715, 0.0035, 0.1015), COACH_TRIM)
	b.box(Vector3(0, 0.0905, -0.01), Vector3(0.0715, 0.0035, 0.1015), COACH_TRIM)
	b.box(Vector3(0, 0.0935, -0.01), Vector3(0.074, 0.004, 0.104), COACH_BODY, COACH_BODY.lightened(0.08))
	# the windows on each side, with a passenger's head and hat in each (a coat figure at the window)
	for sx in [-1.0, 1.0]:
		var x2: float = sx
		for sz in [-0.034, 0.014]:
			var z: float = sz
			b.box(Vector3(x2 * 0.0355, 0.0735, z), Vector3(0.002, 0.024, 0.026), GLASS)
			b.blob(Vector3(x2 * 0.027, 0.074, z), Vector3(0.0072, 0.0078, 0.0072), 6, 3, SKIN, 0.0)
			b.prism(Vector3(x2 * 0.027, 0.0805, z), 0.0085, 0.0085, 0.002, 7, BLACK, BLACK)
			b.prism(Vector3(x2 * 0.027, 0.0818, z), 0.0058, 0.0052, 0.008, 6, BLACK, BLACK)
	# the roof: luggage trunks and a bundle under a rope net
	b.box(Vector3(0, 0.1005, -0.026), Vector3(0.044, 0.012, 0.03), LEATHER, LEATHER.lightened(0.06))
	b.box(Vector3(0.006, 0.1105, -0.012), Vector3(0.03, 0.01, 0.02), LEATHER_DARK, LEATHER)
	b.blob(Vector3(-0.012, 0.1085, 0.012), Vector3(0.014, 0.0095, 0.012), 5, 2, Color("#cdbf9f"), 0.06, Color("#b9a982"))
	# the coach box in front: a seat with a leather hammercloth, and the coachman on it
	b.box(Vector3(0, 0.068, 0.065), Vector3(0.05, 0.022, 0.026), COACH_BODY, COACH_BODY.lightened(0.08))
	b.box(Vector3(0, 0.0805, 0.0655), Vector3(0.052, 0.004, 0.028), LEATHER_DARK)
	var save := b.xf
	b.xf = b.xf * Transform3D(Basis.IDENTITY.scaled(Vector3.ONE * 0.62), Vector3(0.0, 0.074, 0.062))
	_coachman_into(b, true)
	b.xf = save
	# two lamps on brackets at the front corners
	for sx in [-1.0, 1.0]:
		var x3: float = sx
		b.pole(Vector3(x3 * 0.036, 0.07, 0.04), Vector3(x3 * 0.04, 0.078, 0.052), 0.0014, STEEL_DARK, 3)
		b.box(Vector3(x3 * 0.04, 0.0775, 0.056), Vector3(0.007, 0.01, 0.007), STEEL_DARK)
		b.tag = EMISSIVE
		b.box(Vector3(x3 * 0.04, 0.0775, 0.0592), Vector3(0.005, 0.0075, 0.0015), LAMP)
		b.tag = PLAIN
	# the pole and traces to the pair of horses in front, and the horses in harness
	b.pole(Vector3(0, 0.03, 0.05), Vector3(0, 0.05, 0.19), 0.0034, WOOD, 4)
	for sx in [-0.017, 0.017]:
		var x4: float = sx
		b.pole(Vector3(x4 * 1.4, 0.034, 0.05), Vector3(x4, 0.052, 0.17), 0.0012, LEATHER_DARK, 3)
		_horse(b, Vector3(x4, 0.0, 0.2), HORSE if x4 < 0.0 else HORSE_PALE)
	return b.commit()
