shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_disabled, diffuse_burley, specular_schlick_ggx;

#include "res://shaders/common.gdshaderinc"

uniform sampler2D depth_tex : hint_depth_texture, filter_linear;
uniform vec3 shallow_col : source_color = vec3(0.16, 0.62, 0.56);
uniform vec3 mid_col : source_color = vec3(0.05, 0.36, 0.46);
uniform vec3 deep_col : source_color = vec3(0.04, 0.2, 0.34);
uniform float foam_amount = 1.0;

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float depth = texture(depth_tex, SCREEN_UV).r;
	vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth);
	vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
	view.xyz /= view.w;
	vec3 scene_w = (INV_VIEW_MATRIX * vec4(view.xyz, 1.0)).xyz;
	float wd = max(wpos.y - scene_w.y, 0.0);

	// colour by depth (absorption)
	float t1 = 1.0 - exp(-wd * 6.0);
	float t2 = 1.0 - exp(-wd * 1.6);
	vec3 col = mix(shallow_col, mid_col, t1);
	col = mix(col, deep_col, t2);
	float alpha = mix(0.72, 0.97, smoothstep(0.1, 0.45, wd));

	// animated ripples: normal from two scrolling noise fields
	vec2 p = wpos.xz;
	float tt = TIME;
	float e = 0.03;
	float h0 = vnoise(p * 3.0 + vec2(tt * 0.18, tt * 0.11)) + 0.5 * vnoise(p * 7.0 - vec2(tt * 0.25, -tt * 0.2));
	float hx = vnoise((p + vec2(e, 0.0)) * 3.0 + vec2(tt * 0.18, tt * 0.11)) + 0.5 * vnoise((p + vec2(e, 0.0)) * 7.0 - vec2(tt * 0.25, -tt * 0.2));
	float hz = vnoise((p + vec2(0.0, e)) * 3.0 + vec2(tt * 0.18, tt * 0.11)) + 0.5 * vnoise((p + vec2(0.0, e)) * 7.0 - vec2(tt * 0.25, -tt * 0.2));
	vec3 wn = normalize(vec3(-(hx - h0) / e * 0.035, 1.0, -(hz - h0) / e * 0.035));
	NORMAL = normalize((VIEW_MATRIX * vec4(wn, 0.0)).xyz);

	// shore foam: a solid lip plus soft bands that breathe toward the beach
	float n = vnoise(p * 9.0 + tt * 0.3);
	float lip = 1.0 - smoothstep(0.0, 0.012 + 0.012 * n, wd);
	float bands = smoothstep(0.6, 0.9, sin(wd * 70.0 - tt * 1.6 + n * 4.0) * 0.5 + 0.5) * (1.0 - smoothstep(0.015, 0.07, wd));
	float foam = clamp(lip + bands * 0.6, 0.0, 1.0) * foam_amount;

	// winter shore ice in cold latitudes
	float ice = season_snow * smoothstep(snow_line + 0.02, snow_line + 0.12, coldness(wpos) + (n - 0.5) * 0.1) * (1.0 - smoothstep(0.05, 0.2, wd));
	col = mix(col, vec3(0.80, 0.88, 0.93), ice);
	alpha = mix(alpha, 0.95, ice);

	col = mix(col, vec3(0.95, 0.97, 0.96), foam);
	alpha = max(alpha, foam * 0.95);

	ALBEDO = col;
	ALPHA = alpha;
	ROUGHNESS = mix(0.12, 0.6, max(foam, ice));
	SPECULAR = 0.4;
	METALLIC = 0.0;
}
