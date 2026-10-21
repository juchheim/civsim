// Soft contact shadow under each figure so tiny people stay grounded at map scale.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

global uniform float fig_scale;
varying float r;

global uniform float fig_lerp;

void vertex() {
	// custom = (ground offset x, height offset, ground offset z, mode: 0 steady, 1 growing in, 2 fading out)
	vec4 ic = INSTANCE_CUSTOM;
	float t = clamp(fig_lerp, 0.0, 1.0);
	float grow = 1.0;
	if (ic.a == 1.0) {
		grow = smoothstep(0.0, 0.5, t);
	} else if (ic.a == 2.0) {
		grow = 1.0 - smoothstep(0.0, 0.5, t);
	}
	r = length(VERTEX.xz) / 0.022;
	VERTEX.xz *= fig_scale * grow;
	VERTEX += vec3(ic.r, ic.g, ic.b) * (1.0 - t);
}

void fragment() {
	ALBEDO = vec3(0.08, 0.06, 0.04);
	ALPHA = (1.0 - smoothstep(0.2, 1.0, r)) * 0.38;
}
