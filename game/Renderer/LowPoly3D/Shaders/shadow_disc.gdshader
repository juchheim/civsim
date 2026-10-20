// Soft contact shadow under each figure so tiny people stay grounded at map scale.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

global uniform float fig_scale;
varying float r;

void vertex() {
	r = length(VERTEX.xz) / 0.022;
	VERTEX.xz *= fig_scale;
}

void fragment() {
	ALBEDO = vec3(0.08, 0.06, 0.04);
	ALPHA = (1.0 - smoothstep(0.2, 1.0, r)) * 0.38;
}
