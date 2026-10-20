shader_type spatial;
render_mode blend_mix, cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://shaders/common.gdshaderinc"

uniform vec3 civ_col[8];
uniform float territory_strength = 1.0;
uniform vec4 villages[48];      // x, z, radius
uniform int village_count = 0;

varying vec3 wpos;
varying vec3 wnorm;
varying vec2 tuv;
varying vec2 town;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnorm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	tuv = UV;
	town = UV2;
}

void fragment() {
	vec3 c = COLOR.rgb;
	bool wall = tuv.x < -0.5;
	float veg = wall ? 0.0 : COLOR.a;
	float n1 = fbm(wpos.xz * 0.9);
	float n2 = vnoise(wpos.xz * 2.2) * 0.7 + vnoise(wpos.xz * 9.0) * 0.3;
	float n3 = vnoise(wpos.xz * 23.0);
	if (!wall) {
		// two-tone mottling (lusher / drier patches) plus fine grain so flat tops do not read as plastic
		float m = smoothstep(0.3, 0.7, n1);
		vec3 lush = c * vec3(0.82, 0.98, 0.78);
		vec3 dryp = c * vec3(1.08, 1.0, 0.86);
		c = mix(c, mix(lush, dryp, m), veg * 0.7);
		c *= 0.94 + 0.10 * n1 + 0.05 * (n3 - 0.5);
		vec3 g = season_grade(c, 2.0, 0.5, wpos);
		c = mix(c, g, veg);
		// keep lit ground a step darker and richer than the props standing on it
		c = mix(c, c * c / max(luma(c), 0.05) * 0.9, 0.35 * veg) * 0.86;
		// trampled earth around villages and camps
		float dmin = 1e3;
		for (int i = 0; i < village_count; i++) {
			dmin = min(dmin, length(wpos.xz - villages[i].xy) - villages[i].z);
		}
		float earth = 1.0 - smoothstep(-0.12, 0.06, dmin + (n2 - 0.5) * 0.18 + (n3 - 0.5) * 0.05);
		vec3 dirt = vec3(0.20, 0.13, 0.07) * (0.8 + 0.4 * n3);
		c = mix(c, dirt, earth * 0.85);
	} else {
		// faint strata on cliff walls
		float band = vnoise(vec2(wpos.y * 18.0, (wpos.x + wpos.z) * 2.0));
		c *= 0.85 + 0.25 * band;
	}
	// snow
	if (wpos.y > -0.02) {
		float sn = snow_amount(wpos, wall ? 0.0 : wnorm.y, n2);
		sn = max(sn, wall ? 0.0 : smoothstep(0.98, 1.06, wpos.y) * smoothstep(0.6, 0.9, wnorm.y));
		// snow: slightly blue-grey with wind-swept patches where grass shows through
		float patchy = smoothstep(0.25, 0.6, vnoise(wpos.xz * 5.0) * 0.6 + n3 * 0.4);
		vec3 snowc = mix(vec3(0.70, 0.75, 0.84), vec3(0.84, 0.88, 0.95), patchy);
		c = mix(c, snowc, sn * mix(0.75, 1.0, patchy));
	}
	// territory: soft tint, stronger with claim
	if (town.x > -0.5 && wpos.y > -0.05) {
		int idx = int(town.x + 0.5);
		vec3 cc = civ_col[idx];
		float k = territory_strength * (0.05 + 0.13 * town.y) * (wall ? 0.4 : 1.0);
		c = mix(c, cc * (0.55 + 0.9 * luma(c) / max(luma(cc), 0.05)) , k);
	}
	ALBEDO = c * cloud_shadow(wpos, TIME);
	ROUGHNESS = 0.92;
	SPECULAR = 0.25;
}
