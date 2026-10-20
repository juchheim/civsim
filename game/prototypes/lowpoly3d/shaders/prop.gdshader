// Props, buildings, figures and animals (MultiMesh). Vertex colour alpha is a material tag
// (see scripts/mesh_builder.gd); INSTANCE_CUSTOM.rgb = civ / species colour or tint, .a = seed.
shader_type spatial;
render_mode blend_mix, cull_disabled, diffuse_burley, specular_schlick_ggx;

#include "res://shaders/common.gdshaderinc"

global uniform float fig_scale;
uniform float wind = 0.0;
uniform bool is_figure = false;
uniform float lod_grow = 0.0;
uniform bool billboard_y = false;

varying vec3 wpos;
varying vec3 wnorm;
varying vec4 custom;
varying float tagv;
varying vec3 opos;

void vertex() {
	custom = INSTANCE_CUSTOM;
	tagv = COLOR.a;
	opos = VERTEX;
	if (is_figure) {
		VERTEX *= fig_scale;
	}
	VERTEX *= 1.0 + max(fig_scale - 1.25, 0.0) * lod_grow;
	if (billboard_y) {
		// turn the flag (local XY plane) to face the camera around its pole
		vec3 cam = INV_VIEW_MATRIX[3].xyz;
		vec3 org = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
		vec3 dw = vec3(cam.x - org.x, 0.0, cam.z - org.z);
		vec3 dl = (inverse(MODEL_MATRIX) * vec4(dw, 0.0)).xyz;
		float a = atan(dl.x, dl.z) + 0.5;
		float ca = cos(a);
		float sa = sin(a);
		VERTEX.xz = vec2(ca * VERTEX.x + sa * VERTEX.z, -sa * VERTEX.x + ca * VERTEX.z);
		NORMAL.xz = vec2(ca * NORMAL.x + sa * NORMAL.z, -sa * NORMAL.x + ca * NORMAL.z);
	}
	vec3 base = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	if (wind > 0.0) {
		float ph = TIME * 1.4 + base.x * 1.7 + base.z * 1.3;
		float k = VERTEX.y * VERTEX.y * wind;
		VERTEX.x += sin(ph) * k;
		VERTEX.z += cos(ph * 0.83) * k * 0.6;
	}
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnorm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 c = COLOR.rgb;
	float tg = tagv;
	float seed = custom.a;
	vec3 emis = vec3(0.0);
	float evergreen_or_grass = 0.0;
	if (tg < 0.125) {
		// civ / species colour
		c = custom.rgb;
	} else if (tg < 0.3125) {
		// emissive (fire, embers)
		emis = c * (2.5 + 1.0 * sin(TIME * 13.0 + seed * 40.0) * sin(TIME * 7.3 + seed * 11.0));
	} else if (tg < 0.4375) {
		// crop: spring sprouts, summer gold, autumn stubble, winter bare
		vec3 sprout = mix(vec3(0.10, 0.26, 0.04), vec3(0.20, 0.32, 0.06), seed);
		vec3 ripe = mix(vec3(0.55, 0.38, 0.09), vec3(0.42, 0.38, 0.12), seed);
		vec3 stubble = vec3(0.40, 0.30, 0.13);
		vec3 bare = c;
		vec3 r = mix(ripe, sprout, season_spring);
		r = mix(r, stubble, season_autumn);
		r = mix(r, bare, season_snow);
		c = r * (0.9 + 0.2 * seed);
	} else if (tg < 0.5625) {
		c *= custom.rgb;
		c = season_grade(c, 0.0, seed, wpos);
	} else if (tg < 0.6875) {
		c *= custom.rgb;
		c = season_grade(c, 1.0, seed, wpos);
	} else if (tg < 0.8125) {
		// cloth / hide: tone varies per instance
		vec3 a = vec3(0.30, 0.17, 0.08);
		vec3 b = vec3(0.50, 0.36, 0.20);
		vec3 d = vec3(0.20, 0.12, 0.07);
		vec3 hide = mix(mix(d, a, smoothstep(0.0, 0.5, seed)), b, smoothstep(0.5, 1.0, seed));
		c = hide * clamp(luma(c) / 0.16, 0.4, 1.6);
	} else if (tg < 0.9375) {
		c = season_grade(c, 2.0, seed, wpos);
	} else {
		c *= 0.94 + 0.12 * seed;
	}
	// snow settles on upward faces in cold places (not on people)
	if (!is_figure && tg > 0.3125) {
		float sn = snow_amount(wpos, wnorm.y, vnoise(wpos.xz * 9.0));
		c = mix(c, vec3(0.80, 0.85, 0.93), sn * 0.9);
	}
	ALBEDO = c * cloud_shadow(wpos, TIME);
	EMISSION = emis;
	ROUGHNESS = 0.88;
	SPECULAR = 0.3;
	if (is_figure) {
		// keep tiny figures crisp: a little rim brightening
		float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
		EMISSION += c * rim * 0.25;
	}
}
