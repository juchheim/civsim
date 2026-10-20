shader_type spatial;
// Illustrated flame: an upright billboard with flickering tongues, three pigment bands and an ink rim.
// Opaque (discard) so it is inked and graded with the rest of the picture.
render_mode unshaded, cull_disabled, shadows_disabled;

#include "res://shaders/common.gdshaderinc"

uniform vec3 ink = vec3(0.02, 0.015, 0.01);
uniform float time_s = 0.0;
uniform float seed = 0.0;
uniform vec3 c_outer = vec3(0.85, 0.22, 0.06);
uniform vec3 c_mid = vec3(1.0, 0.5, 0.08);
uniform vec3 c_core = vec3(1.2, 0.95, 0.45);

void vertex() {
	vec3 right = normalize(cross(vec3(0.0, 1.0, 0.0), INV_VIEW_MATRIX[2].xyz));
	vec3 fwd = normalize(cross(right, vec3(0.0, 1.0, 0.0)));
	float s = length(MODEL_MATRIX[0].xyz);
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(right * s, 0.0), vec4(0.0, s, 0.0, 0.0), vec4(fwd * s, 0.0), MODEL_MATRIX[3]);
}

float flame(vec2 uv, float t, float grow) {
	// uv.x -1..1, uv.y 0..1 ; returns signed distance-ish (negative inside)
	float y = uv.y;
	float n = vnoise(vec2(uv.x * 3.0, y * 4.0 - t * 5.0) + seed) - 0.5;
	float n2 = vnoise(vec2(uv.x * 6.0 + 3.0, y * 7.0 - t * 8.0) + seed) - 0.5;
	float x = uv.x + n * 0.35 * y + n2 * 0.18 * y;
	float w = grow * 0.62 * pow(max(1.0 - y, 0.0), 0.9) * smoothstep(-0.05, 0.25, y + 0.05);
	return abs(x) - w;
}

void fragment() {
	vec2 uv = vec2(UV.x * 2.0 - 1.0, 1.0 - UV.y);
	float t = time_s;
	float d0 = flame(uv, t, 1.0);
	if (d0 > 0.0) discard;
	float d1 = flame(uv * vec2(1.0, 1.35), t + 0.3, 0.62);
	float d2 = flame(uv * vec2(1.0, 1.9), t + 0.6, 0.38);
	vec3 c = c_outer;
	c = mix(c, c_mid, step(d1, 0.0));
	c = mix(c, c_core, step(d2, 0.0));
	float rim = smoothstep(-0.07, -0.02, d0);
	c = mix(c, ink, rim * 0.9);
	ALBEDO = c;
}
