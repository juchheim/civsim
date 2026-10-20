// Final screen grade: soft vignette and a gentle split tone (cool shadows, warm highlights).
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float vignette = 0.32;
uniform vec3 shadow_tint = vec3(-0.015, 0.0, 0.022);
uniform vec3 light_tint = vec3(0.02, 0.008, -0.016);

void fragment() {
	vec3 c = texture(screen_tex, SCREEN_UV).rgb;
	vec2 d = SCREEN_UV - 0.5;
	d.x *= 1.25;
	float v = 1.0 - vignette * smoothstep(0.28, 0.9, length(d));
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	c += shadow_tint * (1.0 - l) + light_tint * l;
	COLOR = vec4(c * v, 1.0);
}
