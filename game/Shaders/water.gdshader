shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque;

uniform float season = 0.0;
uniform sampler2D tile_state : filter_nearest, repeat_disable;

varying vec2 state_uv;

void vertex() {
	state_uv = UV;
}

void fragment() {
	float ripple = 0.025 * sin(UV.x * 90.0 + TIME * 0.45) * cos(UV.y * 60.0 - TIME * 0.35);
	vec3 shallow = vec3(0.16, 0.47, 0.53);
	vec3 deep = vec3(0.045, 0.19, 0.30);
	vec3 winter = vec3(0.54, 0.65, 0.69);
	vec3 c = mix(shallow, deep, clamp(UV.y * 1.5, 0.0, 0.88));
	c = mix(c, winter, step(2.5, season) * 0.58);
	float visibility = texture(tile_state, state_uv).b;
	if (visibility < 0.25) c = mix(c, vec3(0.055, 0.075, 0.095), 0.91);
	else if (visibility < 0.75) c = mix(c, vec3(0.18, 0.22, 0.25), 0.55);
	ALBEDO = c * COLOR.rgb + ripple;
}
