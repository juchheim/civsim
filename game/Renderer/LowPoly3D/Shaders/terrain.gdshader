shader_type spatial;
render_mode blend_mix, cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://Renderer/LowPoly3D/Shaders/common.gdshaderinc"

uniform vec3 civ_col[8];
uniform sampler2D tile_state : filter_nearest;
uniform sampler2D overlay_state : filter_nearest;
uniform float territory_strength = 1.0;
uniform float claim_overlay_strength = 0.0;
uniform float overlay_forecast = 0.0;
uniform int overlay_mode = 0; // 0 none, 1 claim strength, 2 food security, 3 methods
uniform float terrain_detail = 1.0;
uniform vec4 villages[48];      // x, z, radius
uniform int village_count = 0;

varying vec3 wpos;
varying vec3 wnorm;
varying vec2 tuv;
	varying vec2 tile_uv;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnorm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	tuv = UV;
	tile_uv = UV2;
}

void fragment() {
	vec3 c = COLOR.rgb;
	vec4 state = texture(tile_state, tile_uv);
	int owner = int(state.r * 255.0 + 0.5) - 1;
	int claimant = int(state.a * 255.0 + 0.5) - 1;
	float claim = state.g;
	float visibility = state.b;
	bool wall = tuv.x < -0.5;
	float veg = wall ? 0.0 : COLOR.a;
	float n1 = 0.5;
	float n2 = 0.5;
	float n3 = 0.5;
	if (!wall) {
		n1 = terrain_detail > 0.75 ? fbm(wpos.xz * 0.9) : vnoise(wpos.xz * 0.9);
		if (terrain_detail > 0.25) {
			n2 = vnoise(wpos.xz * 2.2) * 0.7 + vnoise(wpos.xz * 9.0) * 0.3;
			n3 = vnoise(wpos.xz * 23.0);
		} else {
			n2 = n1;
			n3 = n1;
		}
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
		float sn = 0.0;
		if (season_snow > 0.001) {
			sn = snow_amount(wpos, wall ? 0.0 : wnorm.y, n2);
		}
		sn = max(sn, wall ? 0.0 : smoothstep(0.98, 1.06, wpos.y) * smoothstep(0.6, 0.9, wnorm.y));
		if (sn > 0.001) {
			// snow: slightly blue-grey with wind-swept patches where grass shows through
			float patchy = smoothstep(0.25, 0.6, vnoise(wpos.xz * 5.0) * 0.6 + n3 * 0.4);
			vec3 snowc = mix(vec3(0.70, 0.75, 0.84), vec3(0.84, 0.88, 0.95), patchy);
			c = mix(c, snowc, sn * mix(0.75, 1.0, patchy));
		}
	}
	// territory: soft tint, stronger with claim
	if (owner >= 0 && owner < 8 && wpos.y > -0.05) {
		vec3 cc = civ_col[owner];
		float k = territory_strength * (0.05 + 0.13 * claim) * (wall ? 0.4 : 1.0);
		c = mix(c, cc * (0.55 + 0.9 * luma(c) / max(luma(cc), 0.05)) , k);
	}
	if (overlay_mode == 1 && claimant >= 0 && claimant < 8 && claim > 0.001 && wpos.y > -0.05) {
		vec3 cc = civ_col[claimant];
		float k = min(0.72, claim_overlay_strength * claim * 0.66) * (wall ? 0.4 : 1.0);
		c = mix(c, cc * (0.55 + 0.9 * luma(c) / max(luma(cc), 0.05)), k);
	}
	if (overlay_mode == 2 || overlay_mode == 3) {
		vec4 overlay = texture(overlay_state, tile_uv);
		float code = overlay_mode == 2 ? mix(overlay.r, overlay.g, overlay_forecast) : overlay.b;
		if (code > 0.001 && visibility > 0.9 && !wall) {
			vec3 tint;
			if (overlay_mode == 2) {
				float band = floor(code * 255.0 + 0.5) - 1.0;
				tint = band < 0.5 ? vec3(0.20, 0.55, 0.22) : band < 1.5 ? vec3(0.78, 0.62, 0.16) : band < 2.5 ? vec3(0.83, 0.33, 0.12) : vec3(0.65, 0.08, 0.10);
			} else {
				float rung = clamp((code * 255.0 - 1.0) / 7.0, 0.0, 1.0);
				tint = mix(vec3(0.12, 0.42, 0.61), vec3(0.57, 0.25, 0.70), rung);
			}
			c = mix(c, tint, 0.43);
		}
	}
	// Keep the whole landform legible while suppressing details on unknown and explored tiles.
	if (visibility < 0.1) {
		c = mix(vec3(0.17, 0.23, 0.25), c, 0.24);
	} else if (visibility < 0.9) {
		c *= 0.78;
	}
	ALBEDO = c * cloud_shadow(wpos, TIME);
	ROUGHNESS = 0.92;
	SPECULAR = 0.25;
}
