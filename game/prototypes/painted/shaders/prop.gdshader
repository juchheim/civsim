shader_type spatial;
// Gouache-style props: flat pigment with a soft two-tone light ramp, a painted highlight dab,
// pigment mottling in world space, snow caps and a gentle canopy sway.
render_mode cull_back, specular_disabled;

#include "res://shaders/common.gdshaderinc"

uniform sampler2D paper_tex : filter_linear_mipmap, repeat_enable;
uniform float sway = 0.0;
uniform float flat_shade = 0.0;
uniform float time_s = 0.0;
uniform float tint_strength = 1.0;
uniform vec3 tint_color = vec3(1.0);
uniform float snow_amount = 0.0;
uniform float peak_snow = -1.0; // >= 0: local height above which a peak is snow-capped
uniform vec3 snow_col = vec3(0.86, 0.88, 0.92);

varying vec3 v_world;
varying vec4 v_custom;
varying float v_cloud;
varying vec3 v_local;
varying float v_var;

void vertex() {
	v_custom = INSTANCE_CUSTOM;
	v_var = hash12(floor(MODEL_MATRIX[3].xz * 97.0));
	v_local = VERTEX;
	if (sway > 0.0) {
		vec3 o = MODEL_MATRIX[3].xyz;
		float s = sin(time_s * 1.3 + o.x * 0.7 + o.z * 0.5) * sway * max(VERTEX.y - 0.08, 0.0);
		VERTEX.x += s;
	}
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec3 base = COLOR.rgb;
	vec3 tint = v_custom.rgb;
	vec3 c = mix(base, base * tint * tint_color, COLOR.a * tint_strength);
	c *= mix(vec3(0.9, 0.92, 0.95), vec3(1.08, 1.04, 0.98), v_var); // each prop mixed a little differently
	// pigment mottling and paper tooth
	float m = vnoise(v_world.xz * 18.0 + v_world.y * 9.0);
	float g = texture(paper_tex, v_world.xz * 2.3 + v_world.y).r;
	c *= 0.9 + 0.16 * m + 0.08 * (g - 0.5);
	// snow resting on upward faces
	vec3 nw = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	if (flat_shade > 0.5) {
		vec3 fn = normalize(cross(dFdx(VERTEX), dFdy(VERTEX)));
		if (dot(fn, -VERTEX) < 0.0) fn = -fn;
		NORMAL = fn;
		nw = (INV_VIEW_MATRIX * vec4(fn, 0.0)).xyz;
	}
	float snow = v_custom.a * snow_amount * smoothstep(0.25, 0.6, nw.y + (m - 0.5) * 0.4);
	if (peak_snow >= 0.0) {
		// gullies: vertical pigment streaks down the faces
		float gully = vnoise(vec2(atan(v_local.z, v_local.x) * 5.0, v_local.y * 1.5));
		c *= 0.86 + 0.22 * gully - 0.12 * (1.0 - smoothstep(0.0, 0.35, v_local.y));
		// snow by absolute altitude, so only the tall peaks are capped
		float sl = peak_snow + (vnoise(v_world.xz * 7.0 + v_world.y * 5.0) - 0.5) * 0.12;
		float ps = smoothstep(sl, sl + 0.03, v_world.y) * smoothstep(-0.2, 0.25, nw.y);
		snow = max(snow, ps);
		// streaks of rock showing through the snow on steep faces
		snow *= 1.0 - 0.6 * smoothstep(0.55, 0.75, vnoise(vec2(atan(v_local.z, v_local.x) * 6.0, v_local.y * 3.0)));
	}
	c = mix(c, snow_col, snow);
	// pigment pools toward the silhouette, like a dried wash
	float fres = 1.0 - clamp(dot(NORMAL, normalize(-VERTEX)), 0.0, 1.0);
	c *= 1.0 - 0.28 * fres * fres;
	ALBEDO = c;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
	v_cloud = cloud_shadow(v_world.xz, time_s);
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	// two broad tones joined by a soft edge, like a gouache light side and shade side
	float r = smoothstep(-0.05, 0.35, ndl) * 0.8 + 0.2 * smoothstep(0.55, 0.9, ndl);
	DIFFUSE_LIGHT += LIGHT_COLOR * ATTENUATION * r * v_cloud / PI;
}
