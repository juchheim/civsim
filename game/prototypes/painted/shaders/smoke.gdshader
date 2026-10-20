shader_type spatial;
// Hearth smoke: a column of soft wash puffs that drift up and leeward and fade, darker at the rim
// like a dried watercolour blot.
render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_never, blend_mix;

#include "res://shaders/common.gdshaderinc"

uniform float time_s = 0.0;
uniform float seed = 0.0;
uniform vec3 smoke_col = vec3(0.62, 0.62, 0.66);
uniform float strength = 0.4;

void vertex() {
	vec3 right = normalize(cross(vec3(0.0, 1.0, 0.0), INV_VIEW_MATRIX[2].xyz));
	vec3 fwd = normalize(cross(right, vec3(0.0, 1.0, 0.0)));
	float s = length(MODEL_MATRIX[0].xyz);
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(right * s, 0.0), vec4(0.0, s, 0.0, 0.0), vec4(fwd * s, 0.0), MODEL_MATRIX[3]);
}

void fragment() {
	vec2 uv = vec2(UV.x - 0.5, 1.0 - UV.y); // y 0 bottom .. 1 top
	float a = 0.0;
	float rimv = 0.0;
	for (int k = 0; k < 6; k++) {
		float ph = fract(time_s * 0.12 + float(k) / 6.0 + seed);
		vec2 c = vec2(ph * 0.22 + sin(ph * 6.0 + seed * 5.0) * 0.05, 0.08 + ph * 0.85);
		float r = 0.035 + ph * 0.11;
		float n = vnoise((uv - c) * 9.0 + seed * 3.0 + float(k)) - 0.5;
		float d = length((uv - c) * vec2(1.0, 1.3)) - r * (1.0 + n * 0.5);
		float inside = 1.0 - smoothstep(-0.02, 0.01, d);
		float fade = smoothstep(0.0, 0.15, ph) * (1.0 - smoothstep(0.55, 1.0, ph));
		a = max(a, inside * fade);
		rimv = max(rimv, smoothstep(-0.05, 0.0, d) * inside * fade);
	}
	if (a < 0.01) discard;
	ALBEDO = smoke_col * (1.0 - rimv * 0.25);
	ALPHA = a * strength * (0.75 + 0.25 * rimv);
}
