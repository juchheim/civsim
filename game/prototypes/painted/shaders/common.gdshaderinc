// Shared painterly helpers. hash12 / vnoise / warp / hex functions mirror shaders/bake.glsl exactly,
// so painted colours line up with the baked relief.

const float ROW = 0.8660254;

float hash12(vec2 q) {
	vec3 p3 = fract(vec3(q.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 q) {
	vec2 i = floor(q);
	vec2 f = fract(q);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm3(vec2 q) {
	float s = 0.0, a = 0.5;
	for (int i = 0; i < 3; i++) { s += a * vnoise(q); q = q * 2.03 + vec2(17.1, 9.3); a *= 0.5; }
	return s / 0.875;
}

float fbm5(vec2 q) {
	float s = 0.0, a = 0.5;
	for (int i = 0; i < 5; i++) { s += a * vnoise(q); q = q * 2.03 + vec2(17.1, 9.3); a *= 0.5; }
	return s / 0.96875;
}

vec2 warp(vec2 q) {
	vec2 w = vec2(vnoise(q * 0.85 + vec2(3.1, 7.7)), vnoise(q * 0.85 + vec2(11.7, 1.3))) * 2.0 - 1.0;
	vec2 w2 = vec2(vnoise(q * 2.6 + vec2(5.5, 2.2)), vnoise(q * 2.6 + vec2(8.1, 13.4))) * 2.0 - 1.0;
	return q + w * 0.30 + w2 * 0.09;
}

vec2 axial_center(ivec2 a) {
	return vec2(float(a.x) + float(a.y) * 0.5 + 0.5, float(a.y) * ROW + 0.5 * ROW);
}

ivec2 axial_round(vec2 w) {
	float rf = (w.y - 0.5 * ROW) / ROW;
	float qf = (w.x - 0.5) - rf * 0.5;
	float x = qf, z = rf, y = -x - z;
	float rx = round(x), ry = round(y), rz = round(z);
	float dx = abs(rx - x), dy = abs(ry - y), dz = abs(rz - z);
	if (dx > dy && dx > dz) rx = -ry - rz;
	else if (dy > dz) ry = -rx - rz;
	else rz = -rx - ry;
	return ivec2(int(rx), int(rz));
}

ivec2 axial_to_offset(ivec2 a) {
	int r = a.y;
	return ivec2(a.x + (r - (r & 1)) / 2, r);
}

ivec2 hex_dir(int i) {
	if (i == 0) return ivec2(1, 0);
	if (i == 1) return ivec2(1, -1);
	if (i == 2) return ivec2(0, -1);
	if (i == 3) return ivec2(-1, 0);
	if (i == 4) return ivec2(-1, 1);
	return ivec2(0, 1);
}

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

// Drifting cloud shadows: 1 = clear, lower under a cloud.
uniform float cloud_amount = 0.5;
uniform vec2 cloud_drift = vec2(0.6, 0.25);
float cloud_shadow(vec2 p, float t) {
	vec2 q = p * 0.03 + cloud_drift * t * 0.01;
	float n = fbm3(q + vec2(fbm3(q * 1.7 + 3.0), 0.0) * 0.6);
	float c = smoothstep(0.62 - cloud_amount * 0.2, 0.78 - cloud_amount * 0.2, n);
	return 1.0 - c * 0.32;
}

// Beer-Lambert style glazing: pigment over paper, density 1 gives the pigment itself.
vec3 glaze(vec3 paper, vec3 pigment, float density) {
	vec3 t = clamp(pigment / max(paper, vec3(0.001)), vec3(0.0), vec3(1.0));
	return paper * pow(t, vec3(max(density, 0.0)));
}
