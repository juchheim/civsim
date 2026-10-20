shader_type spatial;
// Full-screen finishing pass: silhouette inking from depth, hand-painted wobble, paper tooth,
// warm chronicle grade, vignette and tilt-shift. Drawn on a quad in front of the camera.
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, skip_vertex_transform;

#include "res://shaders/common.gdshaderinc"

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler2D paper_tex : filter_linear_mipmap, repeat_enable;
uniform vec3 ink = vec3(0.02, 0.015, 0.01);
uniform vec3 paper = vec3(0.88, 0.81, 0.66);
uniform float edge_strength = 0.75;
uniform float tilt_shift = 1.0;
uniform float tilt_center = 0.52;
uniform float tilt_band = 0.5;
uniform float vignette = 1.0;
uniform float saturation = 0.86;
uniform vec3 grade_shadow = vec3(0.98, 0.95, 1.04);
uniform vec3 grade_light = vec3(1.03, 1.0, 0.94);
uniform float wobble = 1.0;
uniform float grain = 1.0;
uniform float hatch = 1.0;

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float lin_depth(vec2 uv, mat4 inv_proj) {
	float d = texture(depth_tex, uv).r;
	vec4 v = inv_proj * vec4(uv * 2.0 - 1.0, d, 1.0);
	return -v.z / v.w;
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 px = 1.0 / VIEWPORT_SIZE;
	// a slight hand-painted wobble on every edge
	vec2 wq = uv * VIEWPORT_SIZE / 6.0;
	vec2 wob = (vec2(vnoise(wq), vnoise(wq + vec2(13.1, 7.3))) - 0.5) * px * 1.6 * wobble;
	vec2 suv = uv + wob;

	// tilt-shift: blur grows away from a horizontal focus band
	float dy = abs(uv.y - tilt_center);
	float blur = tilt_shift * smoothstep(tilt_band * 0.5, tilt_band * 0.5 + 0.3, dy) * 1.6;
	vec3 col;
	if (blur > 0.05) {
		vec3 acc = vec3(0.0);
		for (int i = 0; i < 8; i++) {
			float a = float(i) * 2.399 + 0.5;
			float r = sqrt(float(i) + 0.5) / sqrt(8.0);
			vec2 o = vec2(cos(a), sin(a)) * r * px * exp2(blur) * 1.5;
			acc += textureLod(screen_tex, suv + o, blur * 0.75).rgb;
		}
		col = acc / 8.0;
	} else {
		col = textureLod(screen_tex, suv, 0.0).rgb;
	}

	// silhouette ink from depth discontinuities (relative, so it works at every zoom)
	float d0 = lin_depth(suv, INV_PROJECTION_MATRIX);
	float dl = lin_depth(suv - vec2(px.x, 0.0), INV_PROJECTION_MATRIX);
	float dr = lin_depth(suv + vec2(px.x, 0.0), INV_PROJECTION_MATRIX);
	float du = lin_depth(suv - vec2(0.0, px.y), INV_PROJECTION_MATRIX);
	float dd = lin_depth(suv + vec2(0.0, px.y), INV_PROJECTION_MATRIX);
	float lap = abs(dl + dr + du + dd - 4.0 * d0) / max(d0, 0.001);
	float e = smoothstep(0.012, 0.035, lap) * edge_strength * (1.0 - smoothstep(0.2, 1.0, blur));
	col = mix(col, ink, e * 0.8);

	// ink hatching in deep shade: diagonal strokes, crossed where it is darkest
	float l = dot(col, vec3(0.2126, 0.7152, 0.0722));
	{
		vec2 fc = FRAGCOORD.xy;
		float jit = vnoise(fc / 37.0) * 0.6;
		float s1 = abs(fract((fc.x + fc.y) / 5.0 + jit) - 0.5) * 5.0;
		float s2 = abs(fract((fc.x - fc.y) / 6.0 + jit * 0.7) - 0.5) * 6.0;
		float l1 = 1.0 - smoothstep(0.35, 1.0, s1);
		float l2 = 1.0 - smoothstep(0.35, 1.0, s2);
		float breakup = smoothstep(0.25, 0.55, vnoise(fc / 9.0 + 3.0));
		float shade1 = smoothstep(0.19, 0.08, l) * breakup;
		float shade2 = smoothstep(0.07, 0.025, l) * breakup;
		float hk = max(l1 * shade1 * 0.45, l2 * shade2 * 0.4) * hatch * (1.0 - smoothstep(0.2, 1.0, blur));
		col = mix(col, ink, hk);
	}
	// grade: gentle desaturation, warm lights, cool-violet shade
	col = mix(vec3(l), col, saturation);
	col *= mix(grade_shadow, grade_light, smoothstep(0.05, 0.6, l));
	// paper tooth (screen space so the sheet stays put as the world moves under it)
	float g1 = texture(paper_tex, uv * VIEWPORT_SIZE / 900.0).r;
	float g2 = texture(paper_tex, uv * VIEWPORT_SIZE / 260.0 + 0.37).r;
	float tooth = (g1 - 0.5) * 0.05 + (g2 - 0.5) * 0.06;
	col *= 1.0 + tooth * grain;
	// vignette: warm, slightly burnt sheet edges
	vec2 c = uv - 0.5;
	c.x *= VIEWPORT_SIZE.x / VIEWPORT_SIZE.y;
	float v = smoothstep(0.55, 1.1, length(c) + (fbm3(uv * 5.0) - 0.5) * 0.12);
	col = mix(col, col * vec3(0.72, 0.58, 0.44), v * 0.75 * vignette);
	ALBEDO = col;
}
