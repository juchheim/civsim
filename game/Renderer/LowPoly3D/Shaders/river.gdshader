// River ribbons: UV2.x = 0 water, 1 bank. UV.x across (0..1), UV.y downstream.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_opaque, diffuse_burley, specular_schlick_ggx;

#include "res://Renderer/LowPoly3D/Shaders/common.gdshaderinc"

uniform vec3 water_col : source_color = vec3(0.10, 0.36, 0.44);
uniform vec3 bank_col : source_color = vec3(0.24, 0.27, 0.13);

varying vec3 wpos;
varying float kind;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	kind = UV2.x;
}

void fragment() {
	float across = abs(UV.x - 0.5) * 2.0;
	if (kind > 0.5) {
		vec3 c = bank_col * (0.85 + 0.3 * vnoise(wpos.xz * 20.0));
		c = season_grade(c, 2.0, 0.5, wpos);
		float sn = snow_amount(wpos, 1.0, vnoise(wpos.xz * 9.0));
		c = mix(c, vec3(0.86, 0.9, 0.97), sn * 0.8);
		ALBEDO = c;
		ALPHA = 1.0 - smoothstep(0.55, 1.0, across);
		ROUGHNESS = 0.95;
	} else {
		float n = vnoise(vec2(UV.x * 3.0, UV.y * 1.2 - TIME * 0.9));
		float n2 = vnoise(vec2(UV.x * 7.0 + 3.0, UV.y * 2.5 - TIME * 1.4));
		vec3 col = water_col * (0.9 + 0.2 * n);
		col = mix(col, col * 1.6 + vec3(0.05), smoothstep(0.75, 0.95, n2) * 0.35 * (1.0 - across));
		col = mix(col, vec3(0.55, 0.75, 0.75), smoothstep(0.75, 1.0, across) * 0.5);
		float ice = season_snow * smoothstep(snow_line, snow_line + 0.08, coldness(wpos));
		col = mix(col, vec3(0.80, 0.88, 0.94), ice * 0.85);
		ALBEDO = col;
		ALPHA = 1.0 - smoothstep(0.85, 1.0, across) * 0.6;
		ROUGHNESS = mix(0.05, 0.5, ice);
		SPECULAR = 0.7;
		vec3 wn = normalize(vec3((n2 - 0.5) * 0.25, 1.0, (n - 0.5) * 0.25));
		NORMAL = normalize((VIEW_MATRIX * vec4(wn, 0.0)).xyz);
	}
}
