// Shared noise and season helpers.
global uniform float season_spring;
global uniform float season_autumn;
global uniform float season_snow;
global uniform float season_dry;
global uniform float snow_line;
global uniform vec2 map_lat;

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
	float s = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++) {
		s += a * vnoise(p);
		p = p * 2.03 + vec2(17.1, 9.7);
		a *= 0.5;
	}
	return s;
}

float luma(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }

global uniform float cloud_amount;
// Soft drifting cloud shadows (multiply into albedo). 1 = clear, lower = shaded.
float cloud_shadow(vec3 wpos, float t) {
	if (cloud_amount <= 0.001) {
		return 1.0;
	}
	vec2 p = wpos.xz * 0.045 + vec2(t * 0.006, t * 0.0035);
	float n = vnoise(p) * 0.6 + vnoise(p * 2.3 + 7.0) * 0.3 + vnoise(p * 5.1 + 3.0) * 0.1;
	return 1.0 - cloud_amount * smoothstep(0.52, 0.68, n) * 0.38;
}

// 0 at the equator row, 1 at the map's north/south edge.
float coldness(vec3 wpos) {
	return abs(wpos.z - map_lat.x) / map_lat.y;
}

// Snow amount for an upward-facing surface at wpos (0..1).
float snow_amount(vec3 wpos, float up, float n) {
	float c = coldness(wpos) + max(wpos.y - 0.5, 0.0) * 0.35 + (n - 0.5) * 0.12;
	float s = smoothstep(snow_line, snow_line + 0.1, c);
	return s * smoothstep(0.45, 0.85, up);
}

// Seasonal grade for living vegetation. kind: 0 deciduous, 1 evergreen, 2 grass/ground.
vec3 season_grade(vec3 c, float kind, float seed, vec3 wpos) {
	float l = luma(c);
	float cold = coldness(wpos);
	// spring: fresh yellow-green, a little lighter
	vec3 spring = c * vec3(0.93, 1.05, 0.84);
	// grass and ground: the rains bring a green flush
	vec3 flush = vec3(l * 0.78, l * 1.12, l * 0.42);
	spring = kind > 1.5 ? mix(c, flush, 0.55) : spring;
	// autumn: deciduous turns orange/red/yellow by seed; grass turns ochre; evergreen darkens
	vec3 aut_leaf = mix(vec3(0.62, 0.20, 0.03), vec3(0.75, 0.48, 0.05), seed);
	aut_leaf = mix(aut_leaf, vec3(0.55, 0.09, 0.03), step(0.82, seed));
	aut_leaf *= 0.55 + l * 2.2;
	vec3 aut_grass = mix(c, vec3(l * 1.55, l * 1.0, l * 0.32), 0.8);
	vec3 aut_ever = c * vec3(0.92, 0.95, 0.9);
	vec3 autumn = kind < 0.5 ? mix(c, aut_leaf, 0.85) : (kind < 1.5 ? aut_ever : aut_grass);
	// dry season / winter in warm latitudes: straw and dusty
	vec3 dry = mix(c, vec3(l * 1.4, l * 1.2, l * 0.7), 0.7);
	// winter in cold latitudes: deciduous bare (grey-brown), grass dull
	vec3 bare = vec3(l * 0.9, l * 0.75, l * 0.6) * 0.8;
	vec3 winter_cold = kind < 0.5 ? mix(c, bare, 0.85) : (kind < 1.5 ? c * vec3(0.8, 0.88, 0.9) : mix(c, vec3(l) * vec3(0.95, 0.9, 0.75), 0.6));
	float coldw = smoothstep(snow_line - 0.15, snow_line + 0.05, cold);
	vec3 winter = mix(dry, winter_cold, coldw);
	vec3 r = c;
	r = mix(r, spring, season_spring);
	r = mix(r, autumn, season_autumn);
	r = mix(r, winter, season_snow);
	r = mix(r, dry, season_dry * (1.0 - coldw) * (kind > 1.5 ? 0.6 : 0.25));
	return r;
}
