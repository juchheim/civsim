shader_type spatial;
// Draped ink ribbons: rivers and civ borders. The ribbon keeps a minimum pixel width so lines stay
// readable when zoomed out, and carries its arc length for dash patterns.
render_mode cull_disabled, unshaded, depth_draw_opaque;

#include "res://shaders/common.gdshaderinc"

uniform int mode = 0; // 0 river, 1 border
uniform float min_px = 2.0;
uniform vec3 ink = vec3(0.10, 0.075, 0.06);
uniform vec3 water = vec3(0.30, 0.50, 0.62);
uniform float light_level = 1.0;
uniform float world_per_px = 0.01; // at the camera target distance, set from the camera each frame
uniform float cam_dist = 30.0;

varying float v_w;
varying vec3 v_world;

void vertex() {
	// UV.x arc length, UV.y side (-1..1); UV2 perpendicular (x, z); CUSTOM0.x half width
	vec4 vc = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	float dist = max(-vc.z, 0.01);
	float px = world_per_px * dist / cam_dist;
	float hw = max(CUSTOM0.x, px * min_px * 0.5);
	v_w = hw;
	VERTEX.xz += UV2 * UV.y * hw;
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec4 v = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	v.xyz *= 0.994; // pull towards the camera so the ribbon never sinks into the slope
	POSITION = PROJECTION_MATRIX * v;
}

void fragment() {
	float across = abs(UV.y);
	float aa = fwidth(UV.y) * 1.2;
	vec3 c;
	float alpha = 1.0;
	if (mode == 0) {
		float wob = (vnoise(v_world.xz * 9.0) - 0.5) * 0.25;
		float edge = smoothstep(0.55 + wob - aa, 0.55 + wob + aa, across);
		vec3 w = water * (0.92 + 0.16 * vnoise(v_world.xz * 3.0));
		c = mix(w, ink, edge * 0.85);
		if (across > 0.98) discard;
	} else {
		vec3 civ = COLOR.rgb;
		int pat = int(COLOR.a * 8.0 + 0.5);
		float s = UV.x / (v_w * 2.0); // arc length in line widths
		float on = 1.0;
		if (pat == 1) { // long dash
			on = step(fract(s / 7.0), 0.68);
		} else if (pat == 2) { // dots
			float f = fract(s / 2.4) * 2.4 - 1.2;
			float r = length(vec2(f, UV.y * 0.5 * 1.0));
			on = 1.0 - smoothstep(0.55 - aa, 0.55 + aa, r);
			across = r / 0.55;
		} else if (pat == 3) { // dash-dot
			float f = fract(s / 7.0) * 7.0;
			on = (f < 3.6 || (f > 4.6 && f < 5.6)) ? 1.0 : 0.0;
		}
		if (on < 0.5) discard;
		float edge = smoothstep(0.62 - aa, 0.62 + aa, across);
		c = mix(civ, ink, edge * 0.9);
		if (across > 1.0) discard;
	}
	ALBEDO = c * light_level;
}
