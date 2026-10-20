## Season-graded pigments. All values are sRGB; callers convert to linear for shaders.
extends RefCounted

const CIV := [Color("#E69F00"), Color("#56B4E9"), Color("#009E73"), Color("#F0E442"),
	Color("#0072B2"), Color("#D55E00"), Color("#CC79A7"), Color("#4D4D4D")]
const CIV_NAMES := ["Orange", "Sky", "Green", "Yellow", "Blue", "Vermilion", "Purple", "Dark"]

const PAPER := Color("#f1e8d4")
const INK := Color("#2a2019")

# primary wash per biome, spring
const BASE := [
	Color("#7fa6b0"), # 0 ocean (unused on land)
	Color("#c2c386"), # 1 coast (painted as shore grass)
	Color("#eef1f0"), # 2 ice
	Color("#c4c1a2"), # 3 tundra
	Color("#7d9478"), # 4 taiga
	Color("#8fa86a"), # 5 temperate forest
	Color("#b0b878"), # 6 open woodland
	Color("#c8c587"), # 7 grassland
	Color("#dcc795"), # 8 savanna
	Color("#6f9a64"), # 9 tropical forest
	Color("#94aa92"), # 10 wetland
	Color("#e6d0a2"), # 11 desert
	Color("#a8997f"), # 12 mountain
	Color("#c4bcae"), # 13 peak
]
const SECOND := [
	Color("#6f98a6"), Color("#d0c992"), Color("#e4eaea"), Color("#b7b996"), Color("#6e8a70"),
	Color("#9db374"), Color("#c0c283"), Color("#c9c88c"), Color("#c4bf82"), Color("#7fa66c"),
	Color("#a2b59e"), Color("#dcc18f"), Color("#b4a68c"), Color("#d4cec4"),
]
const SNOW := [0.0, 0.5, 1.0, 1.0, 0.9, 0.55, 0.45, 0.4, 0.0, 0.0, 0.4, 0.0, 1.0, 1.0]

const SEASON_NAMES := ["spring", "summer", "autumn", "winter"]


static func _shift(c: Color, b: int, season: int) -> Color:
	match season:
		1: # summer: deeper greens, grasses ripen to gold
			if b in [6, 7, 8, 1]:
				return c.lerp(Color("#d2b866"), 0.35)
			if b in [5, 9, 4]:
				return c.darkened(0.08)
			return c
		2: # autumn: woodland and forest turn, grasses tawny
			if b in [5, 6]:
				return c.lerp(Color("#c08a45"), 0.55)
			if b in [7, 1, 3]:
				return c.lerp(Color("#c9a463"), 0.5)
			if b == 8:
				return c.lerp(Color("#c99d5f"), 0.35)
			return c
		3: # winter: dull, cool, pale
			var g := c.lerp(Color("#b3aa98"), 0.5)
			if b in [9, 4]:
				g = c.lerp(Color("#55685c"), 0.3)
			return Color.from_hsv(g.h, g.s * 0.75, g.v * 0.98)
	return c


static func biome_colors(season: int, second: bool) -> PackedColorArray:
	var src: Array = SECOND if second else BASE
	var out := PackedColorArray()
	for b in src.size():
		out.append(_shift(src[b], b, season).srgb_to_linear())
	return out


static func fields(season: int) -> Array:
	match season:
		0: return [Color("#a9ba72"), Color("#c6c486"), Color("#bfb185")]
		1: return [Color("#c3c26c"), Color("#dcc675"), Color("#adb56a")]
		2: return [Color("#d8bb6a"), Color("#cfa860"), Color("#b39a6e")]
	return [Color("#a7957a"), Color("#97836a"), Color("#8a7660")]


## Canopy colours: broadleaf, conifer, tropical, acacia, shrub.
static func canopy(season: int) -> Array:
	match season:
		0: return [Color("#86a463"), Color("#5b7a63"), Color("#5e9160"), Color("#7b8a50"), Color("#9aa56a")]
		1: return [Color("#6f9452"), Color("#526f5a"), Color("#4f8553"), Color("#8c9554"), Color("#8c9a5c")]
		2: return [Color("#cf8c3c"), Color("#4b6a50"), Color("#4c8045"), Color("#b39a52"), Color("#b08548")]
	return [Color("#9d8f96"), Color("#4f6656"), Color("#55805a"), Color("#a59a7a"), Color("#968a88")]
