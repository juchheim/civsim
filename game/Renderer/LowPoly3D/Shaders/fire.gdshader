// Flames: additive, flickering, scale-pulsing per instance (INSTANCE_CUSTOM.a = seed).
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

uniform float intensity = 1.8;
varying float seed;

void vertex() {
	seed = INSTANCE_CUSTOM.a;
	float f = 0.85 + 0.15 * sin(TIME * 17.0 + seed * 31.0) + 0.1 * sin(TIME * 7.0 + seed * 9.0);
	VERTEX.y *= f;
	VERTEX.xz *= 1.0 + 0.08 * sin(TIME * 11.0 + VERTEX.y * 40.0 + seed * 5.0);
	VERTEX.x += sin(TIME * 5.0 + VERTEX.y * 30.0 + seed) * VERTEX.y * 0.25;
}

void fragment() {
	ALBEDO = COLOR.rgb * intensity;
	ALPHA = 1.0;
}
