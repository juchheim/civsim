shader_type spatial;
// Swallow-tailed civ banner: cloth in the civ colour, a cream emblem drawn as a signed-distance shape
// (sun, wave, leaf, star, mountain, flame, moon, antler), inked edges, waving in the wind.
render_mode cull_disabled, specular_disabled;

#include "res://shaders/common.gdshaderinc"

uniform vec3 civ_col = vec3(0.8, 0.4, 0.0);
uniform int emblem = 0;
uniform vec3 ink = vec3(0.02, 0.015, 0.01);
uniform vec3 cream = vec3(0.86, 0.80, 0.66);
uniform float time_s = 0.0;

void vertex() {
	float u = UV.x;
	VERTEX.z += sin(time_s * 3.1 - u * 5.5) * 0.014 * u + sin(time_s * 5.3 - u * 9.0) * 0.004 * u;
	VERTEX.y -= u * u * 0.012;
}

float sd_star(vec2 p, float r, float rf) {
	// 5-point star (iq)
	const vec2 k1 = vec2(0.809016994375, -0.587785252292);
	const vec2 k2 = vec2(-k1.x, k1.y);
	p.x = abs(p.x);
	p -= 2.0 * max(dot(k1, p), 0.0) * k1;
	p -= 2.0 * max(dot(k2, p), 0.0) * k2;
	p.x = abs(p.x);
	p.y -= r;
	vec2 ba = rf * vec2(-k1.y, k1.x) - vec2(0, 1);
	float h = clamp(dot(p, ba) / dot(ba, ba), 0.0, r);
	return length(p - ba * h) * sign(p.y * ba.x - p.x * ba.y);
}

float emblem_sd(vec2 q) {
	// q: centred, y up, emblem radius ~0.3
	if (emblem == 0) { // sun
		float a = atan(q.y, q.x);
		float spike = pow(abs(cos(a * 6.0)), 12.0);
		return min(length(q) - 0.15, max(length(q) - (0.17 + 0.13 * spike), -(length(q) - 0.19)));
	} else if (emblem == 1) { // wave
		float w1 = abs(q.y - 0.08 - 0.07 * sin(q.x * 14.0)) - 0.04;
		float w2 = abs(q.y + 0.1 - 0.07 * sin(q.x * 14.0 + 1.0)) - 0.04;
		return max(min(w1, w2), abs(q.x) - 0.3);
	} else if (emblem == 2) { // leaf
		vec2 r = vec2(q.x * 0.8 + q.y * 0.6, -q.x * 0.6 + q.y * 0.8);
		float leaf = max(length(r - vec2(0.0, -0.17)) - 0.3, length(r - vec2(0.0, 0.17)) - 0.3);
		leaf = max(leaf, abs(r.y) - 0.3);
		float rib = max(abs(r.x) - 0.015, abs(r.y) - 0.24);
		return max(leaf, -rib);
	} else if (emblem == 3) { // star
		return sd_star(q * vec2(1.0, -1.0) + vec2(0.0, 0.02), 0.3, 0.45);
	} else if (emblem == 4) { // mountain
		return max(q.y + 0.18, max(abs(q.x) * 0.9 + q.y - 0.2, -q.y - 0.3));
	} else if (emblem == 5) { // flame
		vec2 f = q + vec2(0.0, 0.08);
		return length(f * vec2(1.0, 0.7 + 0.6 * smoothstep(-0.2, 0.3, f.y))) - 0.2;
	} else if (emblem == 6) { // moon
		return max(length(q) - 0.26, -(length(q - vec2(0.11, 0.06)) - 0.22));
	}
	// antler
	float s = abs(q.x) - 0.03;
	return min(max(s, abs(q.y) - 0.25), max(abs(q.y - 0.05 - abs(q.x) * 0.6) - 0.03, abs(q.x) - 0.25));
}

void fragment() {
	vec2 uv = UV;
	// swallow-tail notch at the fly end
	float notch = (1.0 - uv.x) - abs(uv.y - 0.5) * 0.35;
	if (uv.x > 0.78 && notch < 0.1) discard;
	vec2 q = (uv - vec2(0.38, 0.5)) * vec2(1.5, -1.0);
	float d = emblem_sd(q);
	float aa = fwidth(d) * 1.2;
	vec3 c = civ_col;
	c *= 0.92 + 0.12 * vnoise(uv * 30.0);
	float em = 1.0 - smoothstep(-aa, aa, d);
	float em_ink = 1.0 - smoothstep(0.0, aa * 1.5, abs(d) - aa * 0.4);
	c = mix(c, cream, em);
	c = mix(c, ink, em_ink * 0.85);
	// ink border inside the cloth edge
	float bd = min(min(uv.y, 1.0 - uv.y), min(uv.x, notch - 0.1 + 0.22 * step(0.78, uv.x)));
	float bw = fwidth(uv.y) * 1.6;
	c = mix(c, ink, 1.0 - smoothstep(bw * 0.5, bw * 1.5, bd));
	ALBEDO = c;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}

void light() {
	float ndl = abs(dot(NORMAL, LIGHT));
	DIFFUSE_LIGHT += LIGHT_COLOR * ATTENUATION * (0.55 + 0.45 * ndl) / PI;
}
