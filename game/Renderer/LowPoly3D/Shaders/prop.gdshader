// Props, buildings, figures and animals (MultiMesh). Vertex colour alpha is a material tag
// (see scripts/mesh_builder.gd); INSTANCE_CUSTOM.rgb = civ / species colour or tint, .a = seed.
shader_type spatial;
render_mode blend_mix, cull_disabled, diffuse_burley, specular_schlick_ggx;

#include "res://Renderer/LowPoly3D/Shaders/common.gdshaderinc"

global uniform float fig_scale;
global uniform float fig_lerp;       // 0 just published .. 1 settled: eases gliding actors from their previous spot
uniform vec3 fig_tints[10];
uniform vec3 animal_tints[80];
uniform bool is_animal = false;
uniform float wind = 0.0;
uniform bool is_figure = false;
uniform float lod_grow = 0.0;
uniform bool billboard_y = false;
uniform int burn_hide = 0;          // 1 grass and low plants vanish from burnt ground, 2 trees do (burn_map G / B)

varying vec3 wpos;
varying vec3 wnorm;
varying vec4 custom;
varying float tagv;
varying vec3 opos;

void vertex() {
	custom = INSTANCE_CUSTOM;
	tagv = COLOR.a;
	opos = VERTEX;
	// Gliding actors: custom = (palette index + mode*stride + seed fraction, height offset, ground offset x, z). The offset
	// points from where the instance stands now to where it stood one publish ago and eases away as fig_lerp goes 0 -> 1.
	vec3 glide = vec3(0.0);
	float grow = 1.0;
	float walk = 0.0;
	float walk_seed = 0.0;
	if (is_figure || is_animal) {
		vec4 ic = INSTANCE_CUSTOM;
		float m = floor(ic.r + 0.001);
		float seedv = clamp(fract(ic.r) / 0.98, 0.0, 1.0);
		float mode = 0.0;
		if (is_figure) {
			mode = floor(m / 16.0);
			custom = vec4(fig_tints[int(m - mode * 16.0)], seedv);
		} else {
			mode = floor(m / 128.0);
			custom = vec4(animal_tints[int(m - mode * 128.0)], seedv);
		}
		float t = clamp(fig_lerp, 0.0, 1.0);
		glide = vec3(ic.b, ic.g, ic.a) * (1.0 - t);
		if (mode == 1.0) {
			grow = smoothstep(0.0, 0.5, t);
		} else if (mode == 2.0) {
			grow = 1.0 - smoothstep(0.0, 0.5, t);
		}
		walk = step(0.003, length(vec2(ic.b, ic.a))) * (1.0 - smoothstep(0.88, 1.0, t));
		walk_seed = seedv;
	}
	if (is_figure) {
		VERTEX *= fig_scale;
	}
	VERTEX *= grow;
	if (burn_hide > 0) {
		vec3 origin = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
		vec4 bm = burn_at(origin.xz);
		float gone = burn_hide == 1 ? bm.g : bm.b;
		VERTEX *= 1.0 - smoothstep(0.3, 0.8, gone);
	}
	if (walk > 0.0) {
		// a little hop per step so gliding people read as walking
		VERTEX.y += walk * abs(sin(TIME * 10.0 + walk_seed * 31.0)) * (is_figure ? 0.006 * fig_scale : 0.004);
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
	if (is_figure || is_animal) {
		mat3 m3 = mat3(MODEL_MATRIX[0].xyz, MODEL_MATRIX[1].xyz, MODEL_MATRIX[2].xyz);
		VERTEX += (transpose(m3) * glide) / max(dot(m3[0], m3[0]), 0.0001);
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
